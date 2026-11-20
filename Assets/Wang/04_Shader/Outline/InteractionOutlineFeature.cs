using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// 선택 대상의 깊이를 마스크에 그린 뒤, 바깥 테두리를 화면에 합성한다.
// Wang_Renderer에 등록해 사용하며, 로컬 플레이어의 카메라에만 적용한다.
// PassData는 작업 등록 시 채워 두고 실제 렌더링 함수에 전달하는 C# 데이터 묶음이다.
public sealed class InteractionOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader outlineShader;

    private Material material;
    private OutlinePass outlinePass;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        material = null;
        outlinePass = null;
        if (outlineShader == null)
            return;

        material = CoreUtils.CreateEngineMaterial(outlineShader);
        outlinePass = new OutlinePass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        InteractionOutlineTarget target = InteractionOutlineTarget.Instance;
        if (outlinePass == null || target == null || !target.isActiveAndEnabled)
            return;

        // 로컬 디텍터가 지정한 게임 카메라에만 그린다.
        if (renderingData.cameraData.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType != CameraRenderType.Base ||
            renderingData.cameraData.camera != InteractionOutlineTarget.SelectedCamera)
            return;

        outlinePass.Setup(target);
        renderer.EnqueuePass(outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        outlinePass = null;
    }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int TexelSizeId = Shader.PropertyToID("_OutlineTexelSize");

        private readonly Material material;
        private InteractionOutlineTarget target;

        // 대상 마스크를 그리는 데 필요한 데이터.
        private sealed class MaskPassData
        {
            public InteractionOutlineTarget.Entry[] Entries; // 그릴 Renderer와 서브메시 개수 목록.
            public Material Material;                       // 마스크용 셰이더 Pass 0을 실행할 재질.
            public int CullingMask;                         // 카메라가 그릴 레이어의 비트 마스크.
        }

        // 마스크에서 테두리를 찾아 화면에 합성하는 데 필요한 데이터.
        private sealed class CompositePassData
        {
            public TextureHandle Mask; // 앞 단계에서 만든 마스크 텍스처의 참조.
            public Material Material;  // 합성용 셰이더 Pass 1을 실행할 재질.
            public Color Color;        // 테두리 색상과 알파.
            public int Width;          // 테두리 두께(마스크 기준 1~4픽셀).
            public Vector4 TexelSize;  // (1/가로, 1/세로, 가로, 세로). xy로 픽셀 이동을 UV로 변환.
        }

        public OutlinePass(Material material)
        {
            this.material = material;
            // 기존 월드 비네트보다 먼저 그려 비네트가 테두리도 가릴 수 있게 한다.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(InteractionOutlineTarget target)
        {
            this.target = target;
        }

        //이번 카메라 화면을 그릴 때 어떤 렌더링 작업을 할지 등록하는 함수
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (target == null || !target.isActiveAndEnabled)
                return;

            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!resources.cameraDepthTexture.IsValid())
                return;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            // R 채널에 선택 대상의 깊이를 기록한다. 0은 대상이 없는 픽셀이다.
            descriptor.depthBufferBits = 0;
            descriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = false;
            descriptor.memoryless = RenderTextureMemoryless.None;

            // 테두리를 찾기 위한 임시 텍스처. R에 선택 대상의 보이는 부분의 깊이를 기록한다.
            // 대상이 없는 픽셀은 0이며, 다음 합성 패스에서 이 경계를 읽어 바깥쪽 선을 그린다.
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_InteractionOutlineMask", true);

            // 1단계: 선택 대상만 그려 마스크를 채운다. 원래 화면 색은 건드리지 않는다.
            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                "Interaction Outline Mask", out var passData))
            {
                passData.Entries = target.Entries;
                passData.Material = material;
                passData.CullingMask = cameraData.camera.cullingMask;
                // 벽 뒤에 가려진 부분을 제외하도록 화면 깊이를 읽고, 결과는 마스크에 쓴다.
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                {
                    foreach (var entry in data.Entries)
                    {
                        Renderer renderer = entry.Renderer;
                        if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                            !renderer.gameObject.activeInHierarchy)
                            continue;

                        if ((data.CullingMask & (1 << renderer.gameObject.layer)) == 0)
                            continue;

                        for (int i = 0; i < entry.SubMeshCount; i++)
                            context.cmd.DrawRenderer(renderer, data.Material, i, 0);
                    }
                });
            }

            // 2단계: 마스크 주변에서 테두리를 찾고, 지정한 색으로 게임 화면에 합성한다.
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                "Interaction Outline Composite", out var passData))
            {
                passData.Mask = mask;
                passData.Material = material;
                passData.Color = target.OutlineColor;
                passData.Width = Mathf.Clamp(target.OutlineWidth, 1, 4);
                // PS_Outline에서 주변 픽셀을 찾을 때 쓸 UV 간격(xy)과 마스크 해상도(zw).
                passData.TexelSize = new Vector4(
                    1f / descriptor.width, 1f / descriptor.height,
                    descriptor.width, descriptor.height);

                // 마스크를 읽는 작업임을 등록해, 마스크 생성이 먼저 실행되도록 한다.
                builder.UseTexture(mask, AccessFlags.Read);
                // 테두리가 앞의 벽을 뚫지 않도록 셰이더에서 화면 깊이를 읽겠다고 등록한다.
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                // 테두리를 덧그릴 목적지로 현재 게임 화면을 지정한다.
                // 기존 색을 유지하며 합성하고, 블렌딩 방식은 셰이더의 Blend 설정을 따른다.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);

                // Render Graph가 이 합성 작업을 실행할 때 호출할 그리기 함수를 등록한다.
                builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                {
                    // 이번 테두리 계산에 사용할 색상·두께·픽셀 간격을 셰이더에 전달한다.
                    data.Material.SetColor(ColorId, data.Color);
                    data.Material.SetFloat(WidthId, data.Width);
                    data.Material.SetVector(TexelSizeId, data.TexelSize);

                    // 마스크를 _BlitTexture에 연결하고, Pass 1의 PS_Outline으로 테두리를 화면에 그린다.
                    // (1, 1, 0, 0)은 입력 텍스처의 UV를 확대·이동 없이 전체 영역 그대로 사용한다는 뜻.
                    Blitter.BlitTexture(context.cmd, data.Mask,
                        new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
                });
            }
        }
    }
}
