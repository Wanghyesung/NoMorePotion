Wood / Flat UI 에셋 (텍스트·아이콘 없음)
=====================================
1x = 원본 크기, 2x = 고해상도(2배). 모두 투명 배경 PNG.

[9-slice 권장 여백 (1x 기준, 좌/우/상/하)]
inventory/window_bg.png        24 / 24 / 24 / 32   (아래 10px는 단색 드롭 그림자, 바깥 2px는 테두리 링 포함)
inventory/panel_*.png          12 / 12 / 12 / 12
inventory/tab_active.png        8 /  8 /  8 /  8
inventory/slot_*.png           10 / 10 / 10 / 10
buttons/btn_*.png               8 /  8 /  8 /  8
tooltip/tooltip_bg.png         12 / 12 / 12 / 16   (위 4px 등급 띠, 아래 6px 드롭 그림자 포함)

[레이어 순서 (아래 → 위)]
인벤토리: window_bg → panel_grid / panel_detail → slot_* → rarity_bar_* (슬롯 하단 8px 안쪽, 아래에서 7px)
          → 아이콘 → slot_cooldown_overlay (슬롯 내부 2px 안쪽, 하단 정렬) → 수량 텍스트
상세 패널: detail_icon_frame_<등급> , 스탯 행 사이 stat_divider
툴팁: tooltip_bg → tooltip_divider → tooltip_keycap

[색상]
월넛 #4F3726 · 패널 #241C16 · 슬롯 #2F251E · 크림 텍스트 #F2E8D5 · 황동 #D2AE66 · 위험 #A4553F
등급: 일반 #B9B1A3 · 고급 #8FBF84 · 희귀 #7FAEDB · 영웅 #B79AE0 · 전설 #E0A55E
