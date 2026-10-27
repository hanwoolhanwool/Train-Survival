# Train Survival 인게임 UI 이미지 제작 브리프

작성일 2026-10-27 · 버전 1.4 · **이미지 88종 제작 완료, 기존 인게임 UI에 연결 완료**

달리는 열차에서 자원을 구분하고, 위험을 읽고, 제작과 정비를 빠르게 수행하도록 인게임 이미지를 만든다. **아이템 그림 40종, 상태와 행동 기호 36종, 공통 UI 원본 부품 12종으로 총 88개 출력 파일**을 구성한다. 공통 부품으로 조합하는 UI 컴포넌트는 12종이다. 상태별 색상 변형과 레시피별 복제는 이 수량에 추가하지 않는다.

이 문서는 자원·탄약·음식 23종, 무기·도구·장비·보따리 17종, 상태·행동 기호, 인게임 공통 부품의 제작 기준이다. 건축물 아이콘 9종, 메인 메뉴·로비, 전면 피격·결빙 효과, 월드 텍스처는 별도 작업이다. 기존 [비주얼 UIUX 가이드](Train-Survival-비주얼-UIUX-가이드.md)의 색상·정보 계층을 유지하면서 미정이었던 아이콘과 공통 부품 구성을 구체화한다.

## 1 게임과 화면의 성격

**다정했던 세계가 별에 홀렸고, 승무원들은 멈출 수 없는 열차에서 함께 살아남는다.** 낮에는 집게로 자원을 확보하고 제작·요리·정비하며, 밤에는 열차를 방어한다. 기차는 거칠지만 관리되는 집이다. 화면은 1인칭이고 배경이 계속 이동하므로 작은 장식보다 실루엣과 안정된 명암이 먼저 읽혀야 한다.

아이템은 손으로 칠한 세미 카툰 오브젝트, 기호는 단순한 스텐실, UI 바탕은 그을린 철판의 절제된 해석으로 통일한다. 메뉴 명판의 두꺼운 리벳·체인·장식 테두리를 작은 HUD에 그대로 옮기지 않는다. 재질의 손맛은 큰 면의 색 변화와 모서리 마모에만 남긴다.

### 1.1 참고 우선순위

1. **이미 반입된 실제 모델의 실루엣과 도색** — 아이템의 정체성 기준.
2. 해당 모델의 BaseColor와 기존 자원 생성 브리프 — 색과 생략 가능한 표면 정보.
3. 게임 세계관과 UI 팔레트 — 신규 음식·의류·부품의 디자인 기준.
4. 신규 외형은 이 문서의 개별 지시 — 기존 모델이 없다는 이유로 임의의 사실적 소품을 끼워 넣지 않는다.

실제 모델의 색이 열차의 황동·무쇠 팔레트와 달라도 모델을 우선한다. 집게·리볼버의 파랑·아이보리 도색을 UI 통일을 이유로 갈색 총으로 바꾸면 안 된다.

### 1.2 실제 오브젝트 참고 자료

![현재 프로젝트 모델 참고 시트](references/ingame-ui/ReferenceSheet.png)

이 시트는 저장소 FBX와 BaseColor를 결합한 **형태·도색 확인용 렌더**다. Unity 인게임 조명 캡처나 완성 아이콘이 아니다. 종별 실제 크기를 비교하는 자료도 아니다. 각 오브젝트는 시트 칸에 맞춰 정규화되어 있다.

- 개별 참조: `references/ingame-ui/Ref_<식별자>.png`.
- 원본 모델·텍스처 경로: [참조 목록](references/ingame-ui/references.json).
- 자원 12종 + 조립된 집게 1종 + 리볼버·샷건·망치·보따리 = **17개 참고 렌더**. 집게는 `Weapon_Harpoon_Launcher`와 `Weapon_Harpoon_Claw`의 원래 상대 위치·방향을 유지해 **발사기 앞에 집게발이 결합된 하나의 완성 오브젝트**로 보여 준다. 원본 모델은 18개지만 참고 이미지와 아이템 아이콘에서는 집게를 둘로 나누지 않는다.
- [기존 구조물과 열차 시트](../Game%20design/AssetReference/Ref_Sheet_ExistingAssets.png)는 철판·배관·천 매듭의 형태 참고다. 회색 모델이므로 색을 복사하지 않는다.
- UV가 펼쳐진 BaseColor 이미지는 **색 참고만** 한다. 펼친 조각들을 하나의 물체 형태로 해석하지 않는다.

## 2 공통 색상과 형태

| 역할 | 색 | 적용 |
|---|---|---|
| 패널 바탕 | `#1F1B1A` | 최종 표시 불투명도 88% |
| 경계 | `#4A423C` | 비활성 경계와 구분선 |
| 본문과 기본 기호 | `#F2EAE0` | 텍스트·기호의 기본 표시색 |
| 보조 텍스트 | `#9A9089` | 수량 보조·설명·비활성 사유 |
| 선택과 포커스 | `#C89B4A` | 황동 모서리 강조 |
| 비활성 면 | `#6B6660` | 컨트롤 면, 본문 글자에는 사용하지 않음 |
| 안전 | 면 `#7FA653`, 글자 `#9FC46E` | 안전 상태 |
| 주의 | `#E3B23C` | 임계 접근 |
| 경고 | `#DD7A2E` | 행동 필요 |
| 위험 | 면 `#B23A2E`, 글자 `#F0705F` | 즉시 대응 |

위 값은 `UiPalette.cs`와 일치시킨다. 지역이 바뀌어도 UI 색의 의미는 바뀌지 않는다. 보라·청록은 별씨와 유적 아이템의 고유색이며, 일반 버튼 선택색으로 쓰지 않는다. 아이템 고유색을 위험색으로 덮어 씌우지 말고 주변 기호·테두리로 상태를 표현한다.

### 2.1 아이템 그림 규격

| 항목 | 확정값 |
|---|---|
| 최종 파일 | 투명 배경 RGBA PNG, 512×512, sRGB |
| 원본 | 1024×1024 이상에서 제작 가능. 최종 512로 내린 결과를 검수 |
| 화면 표시 | 인벤토리 48×48, 핫바 56×56, 제작 재료·획득 토스트 32×32 |
| 안전 여백 | 상하좌우 최소 40px. 긴 변의 실루엣 점유 78~84% |
| 시점 | 약한 원근의 사선 시점. 음식은 내용물이 보이도록 위에서 25~35도. 의류는 정면에 가까운 시점 |
| 긴 물건 | 좌하단 손잡이 → 우상단 기능부 방향을 기본으로 하되 모델의 좌우 구조를 뒤집지 않음 |
| 명암 | 좌상단의 부드러운 형태 음영만. 배경·바닥 그림자 없음 |
| 외곽 | 명도 구분용 어두운 가장자리 8~12px 수준. 검은 만화 테두리가 형태보다 굵어지지 않음 |
| 디테일 | 주 덩어리 2~5개, 넓은 색면 3~5개. 32px에서 사라지는 나사·미세 금·글자는 제거 |
| 효과 | 아이템 내부의 작은 밝은 별씨 면은 허용. 외곽 광채·입자·연무는 금지 |
| 분리 | 슬롯·선택선·수량·키·등급 숫자는 그림에 넣지 않음 |

물건 묶음 자체가 아이템인 목재·돌·탄약은 여러 조각을 하나의 응집된 실루엣으로 그린다. 따로 떨어진 소품을 둘러놓는 연출은 금지한다. 슬롯별 시각 무게는 맞추되 목재와 원목의 차이를 단순 크기 차이만으로 표현하지 않는다.

**집게 방향은 리볼버 참고 이미지에 맞춘다.** 손잡이는 아래쪽, 결합된 집게발은 오른쪽 위를 향하게 한다. 두 부품의 상대 위치를 유지한 채 시점을 조정하며, 이미지 좌우 반전으로 맞추지 않는다. 집게발이 몸통에 가려지지 않도록 리볼버보다 약간 측면에 가까운 시점을 허용한다.

## 3 자원 탄약 음식 23종

파일명은 `I_Res_<식별자>.png`다. **실물**은 개별 참고 렌더를 첨부하고, **신규**는 아래 외형을 이번 제작 기준으로 사용한다. 괄호 안 영문은 공통 프롬프트의 OBJECT 뒤에 붙일 지시다.

| 식별자 | 이름 | 참조 | 그릴 내용과 반드시 구분할 특징 |
|---|---|---|---|
| Wood | 목재 | 실물 `Ref_Wood` | 끈으로 묶은 짧은 통나무 3개, 밝은 절단면. 판자 묶음으로 바꾸지 않음. (three short cut logs tied with one thick rope) |
| Stone | 돌 | 실물 `Ref_Stone` | 중성 회색 돌무더기, 큰 돌 2개와 작은 받침 돌. 광맥 줄무늬 없음. (a compact cluster of plain rounded angular gray stones) |
| Scrap | 고철 | 실물 `Ref_Scrap` | 적갈색 녹슨 판금과 굽은 파이프, 구멍·접힘 한두 곳. 정제 주괴 금지. (rusted bent iron plates and one thick curved pipe) |
| Niter | 화약 원료 | 실물 `Ref_Niter` | 흰 모암 위 굵은 노란 결정. 검은 화약 가루·화약통 금지. (white mineral base with chunky yellow crystals) |
| RawFood | 식재료 | 실물 `Ref_RawFood` | 빨간 버섯·열매와 초록 잎. 생고기나 닭다리로 바꾸지 않음. (red mushrooms and berries on broad green leaves) |
| Timber | 원목 | 실물 `Ref_Timber` | 굵은 통나무 한 개, 어두운 수피·작은 가지 절단부·큰 단면. 목재의 끈 없음. (one thick dark bark log with a cut branch stub) |
| OreVein | 광맥 | 실물 `Ref_OreVein` | 어두운 큰 암석을 가로지르는 푸른 회색 광맥 띠 2개. 돌보다 높은 덩어리. (a dark bulky rock crossed by two broad blue gray ore bands) |
| Rice | 벼 | 실물 `Ref_Rice` | 묶은 황금색 이삭, 늘어진 곡립 덩어리. 쌀밥 그릇과 구분. (a tied sheaf of golden rice stalks with drooping grain heads) |
| Salt | 소금 | 실물 `Ref_Salt` | 낮고 넓은 백색 결정층, 연한 청색 그림자. 얼음처럼 높은 송곳 모양 금지. (a low broad slab of pale white salt crystals) |
| BossCore | 보스 핵 | 신규 | 둥글고 응축된 보랏빛 결정 중심과 어두운 껍질 조각 3개. 금속 기계 링 금지. (a compact violet crystal core held by three dark mineral fragments) |
| Ice | 얼음 | 실물 `Ref_Ice` | 높고 뾰족한 푸른 얼음 덩어리와 넓은 밝은 균열면. 소금의 낮은 형태와 대비. (one tall blue ice shard with broad pale fracture planes) |
| RareMetal | 희귀 금속 | 실물 `Ref_RareMetal` | 은백색·옅은 보라의 층진 결정 광석. 금괴·작은 보석 금지. (a stepped cluster of silver white mineral blades with pale violet faces) |
| RelicPart | 유적 부품 | 실물 `Ref_RelicPart` | 낮은 원형 고대 기계, 청록 중심과 반복된 금속 외륜. 보스 핵과 달리 인공적 원형 구조 유지. (a squat circular ancient machine part with a cyan center and segmented metal rim) |
| Fish | 생선 | 신규 | 통통한 은청색 물고기 한 마리, 넓은 꼬리·밝은 배. 붉은 눈·별씨 없음. 적 물고기 모델을 재사용하지 않음. (one plump silver blue edible fish with a broad tail and ordinary dark eye) |
| RevolverAmmo | 권총탄 | 신규 | 짧고 둥근 탄두의 황동 탄약 3발. 원통형 총몸과 혼동하지 않음. (three short brass cartridges with rounded bullet tips) |
| ShotgunAmmo | 산탄 | 신규 | 굵은 적갈·주황 종이 탄피 2발, 황동 바닥·평평한 끝. (two thick rust orange shotgun shells with brass bases and flat crimped tops) |
| RifleAmmo | 소총탄 | 신규 | 길고 가는 뾰족 탄두 2발, 목이 좁아지는 탄피와 청록 식별 띠. (two long bottleneck rifle cartridges with pointed tips and muted teal bands) |
| CookedMeal | 구운 식사 | 신규 | 낮은 금속 접시에 노릇한 버섯·구운 채소. 생식재료와 조리 전후가 이어짐. (roasted mushrooms and browned vegetables on a shallow metal plate) |
| HeartyStew | 든든한 스튜 | 신규 | 양쪽 작은 손잡이가 있는 깊은 법랑 볼, 짙은 국물과 큰 건더기. 증기 없음. (a deep enamel bowl with two small side handles filled with dark chunky stew) |
| CookedRice | 밥 | 신규 | 작은 둥근 그릇 위 높게 담긴 아이보리 밥. 쌀알을 낱개로 세밀하게 묘사하지 않음. (a small round bowl holding a mound of warm ivory cooked rice) |
| PreservedMeal | 보존식 | 신규 | 각진 갈색 기름종이 포장과 십자 끈. 회색 둥근 보따리와 구분. (a rectangular brown waxed paper food parcel tied with crossed twine) |
| PurifiedWater | 식수 | 신규 | 투명한 짧은 유리병과 코르크, 푸른 물 면. PET병·갈증 상태 표시 금지. (a squat clear glass bottle with a cork and a pale blue water line) |
| WarmingTea | 온차 | 신규 | 한쪽 손잡이의 연녹색 법랑 머그, 호박색 차. 스튜의 양손잡이·넓은 그릇과 구분. (a muted green enamel mug with one handle and amber tea) |

**보스 핵 색 결정:** 아이콘의 중심은 별씨 보라 `#8E7BE8`로 통일한다. 현재 ResourceCatalog의 색 `#9E298C`와는 차이가 있으므로 실제 UI 연결 시 데이터 색을 함께 정합화한다. 이 문서 작성으로 게임 데이터가 변경되지는 않는다. 유적 부품은 실제 모델의 청록을 보존한다.

## 4 무기 도구 장비 17종

파일명은 `I_Item_<식별자>.png`다. 기존 모델이 있는 항목은 **종류명보다 실물 외형이 우선**이다.

| 식별자 | 이름 | 참조 | 그릴 내용과 반드시 구분할 특징 |
|---|---|---|---|
| Harpoon | 집게 | 조립 실물 `Ref_Harpoon` | **발사기와 집게발이 결합된 완성 집게 한 개.** 파랑·아이보리 통형 발사기, 주황 레버·손잡이 끝, 발사구 앞에 이어진 주황·검정 집게발을 모두 보존한다. **리볼버와 같은 화면 방향으로 손잡이는 아래, 집게발은 오른쪽 위**를 향한다. 발사기만 그리거나 집게발을 옆에 분리 배치하지 않는다. (one fully assembled harpoon tool: the exact blue and ivory launcher with orange controls AND its orange and black claw attached at the front, preserving the reference assembly; match the revolver's screen direction, grip down and claw pointing upper-right, no mirroring) |
| Revolver | 리볼버 | 실물 `Ref_Revolver` | 각진 아이보리 상부·파란 뒤쪽과 손잡이 끝·회색 손잡이, 주황 작은 조작부. 서부극 목제 리볼버나 보이지 않는 회전 실린더를 추가하지 않음. (the exact chunky ivory and blue sidearm from the reference, preserve its actual silhouette) |
| Hammer | 수리 망치 | 실물 `Ref_Hammer` | 양끝 둥글어진 사각 철제 머리, 짧은 황토·주황 손잡이. 장도리 집게발·렌치로 바꾸지 않음. (a short orange wooden handle and one heavy rectangular iron hammer head) |
| Shotgun | 샷건 | 실물 `Ref_Shotgun` | 올리브색 몸통, 황토색 펌프·개머리판, 어두운 총구와 주황 가늠쇠. 측면을 충분히 보여 펌프와 길이를 읽히게 함. (the olive green shotgun with ochre pump and stock from the reference, shown mostly from the side) |
| Rifle | 라이플 | 신규 | 긴 단일 총열, 길게 뻗은 어두운 목재 개머리판·수동 볼트 손잡이. 샷건보다 길고 가늘며 펌프 없음. (a long bolt action rifle with one slim iron barrel, dark wooden stock and a clear bolt handle) |
| Melee | 마체테 | 신규 | 넓고 짧은 무광 철제 칼날, 둥근 끝, 천 감은 손잡이. 도끼·검·피 묻은 무기 금지. (a broad short matte iron machete with a rounded tip and wrapped grip) |
| LeatherCoat | 가죽 옷 | 신규 | 허리까지 오는 밤색 가죽 외투, 넓은 깃·큰 잠금 2개. 방한 파카의 모피 테두리는 없음. (a waist length brown leather coat with a broad collar and two large fasteners) |
| DesertRobe | 사막 로브 | 신규 | 모래빛 긴 로브, 얇고 넓은 소매·청회색 허리띠. 두꺼운 패딩 표현 없음. (a long sand colored desert robe with loose sleeves and a muted blue gray sash) |
| ScrapHelmet | 고철 투구 | 신규 | 둥근 회색 철제 반모, 녹슨 덧댐 판 하나와 큰 리벳 2개. 뿔·바이저·군 표식 없음. (a rounded scrap iron helmet with one rusted patch plate and two large rivets) |
| PaddedPants | 누비 바지 | 신규 | 황갈색 바지, 굵은 누빔 3줄·무릎 덧댐. 방한 바지와 달리 모피 끝단 없음. (ochre quilted trousers with three broad stitched bands and knee patches) |
| DesertBoots | 사막 장화 | 신규 | 모래빛 중간 높이 부츠 한 쌍, 넓은 밑창과 천 감김. 설상화처럼 부풀리지 않음. (a pair of sand colored mid height boots with broad soles and cloth wraps) |
| Bundle | 보따리 | 실물 `Ref_Bundle` | 회색 둥근 천 주머니, 황토색 굵은 매듭과 초록 덧댐. 배낭·상자·갈색 식량 포장으로 바꾸지 않음. (a round gray cloth bundle with a thick ochre knot and green sewn patches) |
| FurHood | 모피 후드 | 신규 | 부드러운 회갈색 후드, 아이보리 두꺼운 털 테두리, 안쪽 얼굴 부분 비움. 투구와 달리 금속 없음. (an empty gray brown fur hood with a thick ivory fur rim) |
| WinterParka | 방한 파카 | 신규 | 청회색 두꺼운 몸통, 아이보리 털 깃·짧고 큰 소매. 가죽 옷보다 부푼 실루엣. (a bulky slate blue winter parka with an ivory fur collar and thick short sleeves) |
| WinterPants | 방한 바지 | 신규 | 청회색 두꺼운 바지, 넓은 단열 패널과 아이보리 발목 털. (bulky slate blue insulated trousers with broad panels and ivory ankle fur) |
| WinterBoots | 방한 부츠 | 신규 | 짙은 회갈색 통통한 부츠 한 쌍, 두꺼운 밑창·아이보리 털 입구. (a pair of stout dark gray brown winter boots with thick soles and ivory fur cuffs) |
| FishingRod | 낚싯대 | 신규 | 짧게 과장한 황갈색 굵은 낚싯대, 큰 릴·한 번 굽은 끝. 가는 줄과 작은 바늘은 아이콘에서 생략. (a chunky ochre fishing rod with one large reel and a gently curved tip, no thin line) |

장비 9종은 얼굴·손·몸통 마네킹 없이 물건만 그린다. 방한 세트는 회청색과 아이보리 털로 통일하고, 방한 부츠와 사막 장화는 색뿐 아니라 두께·입구 형태도 다르게 한다. 집게 1~3단계는 **같은 아이콘 + 별도 단계 숫자 배지**로 표시한다. 현재 요청에 없는 등급별 무기 원화를 추가하지 않는다.

## 5 상태 행동 조작 기호 36종

**다색 아이템 그림과 다른 체계**다. 단색 흰색 RGBA PNG 128×128로 납품하고 표시 시 색상을 적용한다. 벡터 원본을 함께 보관한다. 화면에서 24px를 기본으로, 조작 안내에서 32px를 사용한다. 24px 표시 기준 선·연결부 최소 2px, 구멍 최소 3px, 둥글되 뭉개지지 않는 끝단으로 통일한다. 128px 원본 안전 여백은 12px다.

손으로 그린 느낌은 외곽의 아주 작은 비대칭으로만 표현한다. 아이콘 안의 그라데이션·사진 질감·광채·글자는 넣지 않는다. 상태·행동·시스템·장비 슬롯을 각 접두어로 분리한다.

### 5.1 상태 13종

파일명 `G_Status_<식별자>.png`.

| 식별자 | 의미 | 형태와 적용 |
|---|---|---|
| Health | 체력 | 넓은 하트. 실제 수치와 바를 병기 |
| Hunger | 허기 | 단순한 볼과 짧은 숟가락. 특정 음식 그림과 구분 |
| Temperature | 체온 | 굵은 온도계. 숫자의 ℃는 텍스트 |
| Cold | 추위 | 단순 눈송이 6갈래. 낮은 체온 설명에 사용 |
| Heat | 더위 | 태양 원반과 짧은 열선 3개. 낮 시간 기호와 형태 구분 |
| Frostbite | 동상 | 발끝 실루엣에 작은 얼음 모서리. 부위·단계는 텍스트 |
| Fuel | 연료 | 작은 화구 안의 불꽃. 재생·보온 기호와 구분 |
| Ammo | 탄약 상태 | 탄약통의 열린 입구와 평행 막대 2개. 탄약 종류는 해당 자원 아이콘을 함께 사용 가능 |
| Regen | 재생 버프 | 하트 옆의 작은 위쪽 화살표. Health와 구분 |
| Warmth | 보온 버프 | 몸통을 감싸는 넓은 목도리. 불꽃을 복제하지 않음 |
| DetachedCar | 이탈 칸 | 단순 객차와 끊긴 연결부. 연결 고리만 그린 Recouple과 구분 |
| Day | 낮 | 속이 빈 원과 균등한 짧은 광선. 새벽·황혼은 같은 기호와 국면 문구 사용 |
| Night | 밤 | 초승달과 작은 별 하나 |

현재 구현된 시간제 버프는 **재생과 보온 2종**이다. 별도 갈증 스탯은 없고 식수는 요리 재료이므로 갈증 기호를 만들지 않는다. 산소·출혈·중독·방사능 등의 상태도 이번 범위에 추가하지 않는다. 연료는 월드 표현이 안정화될 때까지 현재 HUD 정보를 유지하되 기호를 준비한다.

### 5.2 행동 11종

파일명 `G_Action_<식별자>.png`. 행동 기호는 입력 키를 대신하지 않는다. **대상 이름 + 실제 입력 키 + 행동/결과**를 함께 표시한다.

| 식별자 | 의미 | 형태 |
|---|---|---|
| Interact | 열기와 일반 사용 | 손바닥과 짧은 접근선 |
| Grab | 집게 채집과 회수 | 실물 집게발을 단순화한 벌어진 두 턱 |
| Repair | 수리 | 실물 망치의 사각 머리와 짧은 손잡이 |
| Build | 설치와 증축 | 판자 두 장 옆의 더하기 |
| Demolish | 철거 | 판자 두 장 옆의 빼기. Repair와 같은 망치만 쓰지 않음 |
| Recouple | 칸 재결합 | 마주 보는 굵은 연결 고리 2개 |
| Refuel | 연료 투입 | 화구를 향해 들어가는 굵은 화살표 |
| Craft | 제작 | 작은 작업대 상판과 공구 하나 |
| Cook | 요리 | 손잡이 냄비와 뚜껑. Hunger의 볼과 구분 |
| Reload | 재장전 | 굵은 탄약 하나를 감싸는 열린 회전 화살표 |
| FishBite | 입질과 챔질 | 굵은 낚시찌와 짧은 튀는 선 2개. 느낌표는 별도 Warning 재사용 |

### 5.3 시스템 기호 8종

파일명 `G_System_<식별자>.png`.

| 식별자 | 의미 | 형태와 재사용 |
|---|---|---|
| Warning | 주의와 거부 사유 | 둥근 삼각형 안 느낌표. 위험 단계는 표시색과 문구로 구분 |
| Confirm | 성공과 장착됨 | 굵은 체크 |
| Cancel | 닫기와 불가 | 짧고 굵은 X. 단독 사용 시 버튼에 접근 가능한 이름 제공 |
| Lock | 실제 이용 불가 | 둥근 자물쇠. 단순 재료 부족에는 사용하지 않음 |
| Chevron | 접기와 펼치기 | 굵은 갈매기 한 개. 회전하여 상하좌우 재사용 |
| MouseLeft | 좌클릭 | 마우스 외곽과 왼쪽 버튼 채움 |
| MouseRight | 우클릭 | 마우스 외곽과 오른쪽 버튼 채움 |
| Drag | 끌어놓기 | 작은 슬롯과 짧은 이동 화살표. 입력 도움말에만 사용 |

E·R·X·Esc·숫자 1~5·Ctrl 등 키 이름은 공통 키캡 위의 텍스트다. 키마다 이미지를 만들지 않는다. 조작 변경 시 표시 문자열도 실제 바인딩을 따라야 한다.

### 5.4 빈 장비 슬롯 4종

파일명 `G_Equip_<식별자>.png`.

| 식별자 | 부위 | 형태 |
|---|---|---|
| Head | 머리 | 단순 두상 옆면 |
| Body | 상체 | 짧은 소매의 몸통 |
| Legs | 하체 | 두 갈래 바지 |
| Feet | 신발 | 단순 부츠 한 짝 |

아이템 장착 시 빈 슬롯 기호를 숨기고 실제 장비 아이콘을 표시한다. 동상 부위 안내에서도 같은 기호를 재사용할 수 있다.

## 6 공통 UI 구성 확정

![공통 UI 구성 검토 시트](references/ingame-ui/UIComponentBoard.png)

시트의 물건은 실제 모델 참고 렌더를 임시 배치한 것이다. 기호와 최종 아이템 그림 제작 전의 구성 검토용이며, 게임 화면 캡처가 아니다. 수량·상태는 예시이고, 게이지의 채움 비율과 위험 단계는 독립적으로 결정된다.

### 6.1 전체 규칙

- 기준 화면 1920×1080. 문서의 표시 크기는 이 기준의 UI 픽셀이다. 1440p에서는 비례 확대하고 16:10·21:9에서는 모서리 기준 배치를 유지한다.
- 기본 간격 단위 8px, 패널 안쪽 여백 20px, 패널 간격 16px, 화면 가장자리 여백 24px.
- 글자는 Noto Sans KR 계열의 현재 프로젝트 폰트와 TMP를 사용한다. 본문 20px, 보조 16px, 수량·키 최소 14px, 제목 24px.
- 손·무기가 차지하는 하단 중앙을 비워 둔다. 상태는 좌하단, 핫바는 우하단, 탄약은 핫바 위, 사건 배너는 상단 중앙에 둔다.
- 패널 바탕의 **최종 알파가 0.88**이 되게 한다. 원본 알파 0.88에 표시 알파 0.88을 다시 곱하지 않는다.
- 정상 체력의 물러남은 게이지·기호·보조값 레이어에 적용한다. 공통 패널 배경까지 통째로 반투명하게 만들어 흐르는 배경이 비치게 하지 않는다.
- 색상 선택과 위험은 분리한다. 선택 = 황동 모서리, 위험 = 기호·위험 테두리·사유 문구. 확대 효과는 기본으로 쓰지 않는다.

### 6.2 공통 원본 이미지 12종

파일명은 `U_<식별자>.png`. **원본 12장은 모든 화면에서 재사용**한다. 단순한 도형과 기호는 벡터 또는 UI 드로잉으로 정확히 제작하고, 이미지 생성은 표면의 아주 약한 손맛을 찾는 용도로만 사용한다. 생성 결과에 정확한 모서리 치수·투명 영역을 맡기지 않는다.

9슬라이스는 모서리를 고정하고 가장자리와 가운데만 늘리는 방식이다. 아래 경계값은 최종 원본 픽셀의 **왼쪽·아래·오른쪽·위** 순서다.

| 식별자 | 원본 크기 | 슬라이스 경계 | 내용과 용도 |
|---|---|---|---|
| PanelBody | 128×128 | 16·16·16·16 | 둥근 사각 바탕 마스크. 외곽만 투명, 중앙은 균일. 창·툴팁·토스트·배너 공용 |
| PanelHeader | 256×64 | 16·8·16·8 | 낮은 높이의 머리글 띠. 가장자리 미세한 철판 마모, 글자 없음 |
| SlotBase | 128×128 | 16·16·16·16 | 슬롯 내부 바탕. 모서리만 약하게 깎고 중앙은 평평함 |
| SlotOutline | 128×128 | 16·16·16·16 | 중앙 완전 투명한 얇은 슬롯 윤곽. 정상·경고·드롭 판정에 재사용 |
| FocusCorners | 128×128 | 20·20·20·20 | 중앙·변 중앙은 투명, 네 귀퉁이의 짧고 굵은 ㄱ자. 선택 전용 |
| ControlFace | 192×64 | 12·12·12·12 | 버튼·탭·레시피 행·스크롤 손잡이의 낮은 면 |
| GaugeTrack | 128×32 | 12·8·12·8 | 속이 빈 얕은 게이지 홈. 패널보다 한 단계 어둡게 표시 |
| GaugeFill | 64×16 | 6·4·6·4 | 흰색 채움 마스크. 종류·단계별 표시색 적용 |
| Keycap | 64×64 | 12·12·12·12 | 빈 입력 키 바탕. E·Esc 등 가로 확장, 글자 없음 |
| ProgressRing | 128×128 | 없음 | 중앙 투명한 흰색 원형 띠. 전체 띠를 바탕에 쓰고 같은 원본을 각도에 따라 드러내며 진행 표시 |
| BadgeDisc | 64×64 | 없음 | 작은 원형 배지 바탕. 집게 단계·수량 보조의 텍스트를 별도 배치 |
| Divider | 128×8 | 4·0·4·0 | 가운데가 일정한 얇은 선. 창 구획 구분 |

**출력 공통:** RGBA, sRGB, 흰색 또는 중립 회색의 색상 적용용 마스크, 텍스트 없음. `PanelHeader`·`ControlFace`의 밝기 변화도 중립색으로 제한한다. PNG에 황동이나 위험색을 굽지 않는다. `PanelBody`·`SlotBase`·`GaugeTrack` 내부는 불투명 원본으로 만들고 표시 단계에서 알파를 정한다. 선·모서리 계열은 중앙이 완전 투명해야 한다.

레이아웃 마스크를 먼저 확정하고, 손맛은 색을 입힌 미리보기에서 판단한다. 작은 UI에서 사실적인 녹·깊은 홈·리벳은 제거한다. 그래픽을 반전해서 위아래 모서리가 달라지는 일 없이 좌우·상하 경계를 맞춘다.

### 6.3 조합 컴포넌트 12종

이 표의 크기는 **이번에 확정한 제작·구현 목표**다. 현재 IMGUI 화면의 치수가 모두 이 값이라는 뜻은 아니다.

| 번호 | 컴포넌트 | 기준 표시 크기 | 조합과 동작 |
|---|---|---|---|
| C01 | 아이템 슬롯 | 가방 64×64, 핫바 72×72 | SlotBase + 아이템 + SlotOutline + FocusCorners. 수량 우하단, 키 좌상단, 집게 단계 우상단. 고정 코너 레이블은 아이템 위 불투명 배지로 읽기 보장 |
| C02 | 인벤토리와 보관 창 | 가방 폭 392, 높이 내용에 맞춤 | PanelBody + PanelHeader + Divider + 5열 슬롯. 64×5 + 8×4 + 여백20×2 = 392. 가방·창고·보따리에서 공용 |
| C03 | 장비 슬롯 | 64×64, 4부위 | C01 + G_Equip. 머리·상체·하체·신발 순서, 장착됨 체크는 별도 |
| C04 | 제작 레시피 행 | 폭 392 이상, 최소 높이 88 | ControlFace + 결과 아이콘48 + 이름 + 재료 아이콘32 + 비용 + 제작 버튼. 재료가 많으면 줄을 늘려 높이 확장. 이름·재료와 버튼이 겹치지 않게 버튼 영역 고정 |
| C05 | 상태와 보스 게이지 | 일반 240×16 + 기호24, 보스 폭520 | GaugeTrack + GaugeFill + 상태 기호 + 텍스트. 가로 전체 크기를 유지한 채 채움 영역을 잘라 비율 표현. 0%에서 채움은 완전히 숨김 |
| C06 | 무기와 탄약 표시 | 184×56 | 탄약 자원 아이콘32 + 현재/장전 용량 + 예비탄. 재장전은 Reload와 진행률. 무기 종류 아이콘은 필요할 때만 표시 |
| C07 | 대상 상호작용 안내 | 최소 240×56, 최대 폭400 | PanelBody + 행동 기호24 + 대상명 + Keycap + 결과. 대상 근처에 배치하고 화면 밖으로 나가지 않게 제한. 흔들림 보정 |
| C08 | 홀드와 작업 진행 | 원형32 또는 가로160×8 | ProgressRing 또는 GaugeTrack/Fill + 취소 가능한 행동명. 철거·재장전 등 진행값이 있는 행동에만 사용 |
| C09 | 상세 설명 툴팁 | 폭280~360, 높이 자동 | PanelBody + 이름 + 설명 + 재료/효과. 커서와 선택 항목을 가리지 않는 쪽으로 배치 |
| C10 | 획득과 거부 토스트 | 최소 280×48 | PanelBody + 아이템32 또는 Warning24 + 문구. 여러 획득은 동일 자원끼리 합산 가능. 긴 한국어는 최대 2줄 후 높이 확장 |
| C11 | 사건 배너 | 폭520, 최소 높이56 | PanelBody + PanelHeader + Warning/DetachedCar/시간 기호 + 문구. 상단 중앙, 동시에 최대2개, 이후 큐 처리 |
| C12 | 공통 버튼과 탭 | 버튼 최소96×40, 탭 최소112×40 | ControlFace + 글자 + 필요 시 시스템 기호. 아이콘 전용 닫기 버튼은40×40. 탭은 선택 시 밝은 바탕·어두운 글자 |

**화면 조합:** 핫바 5칸은 72×5 + 8×4 = 392px 폭이다. 가방 창과 제작 창은 두 창을 합친 묶음을 화면 안에 중앙 배치하고 16px 간격을 둔다. 슬롯 수·가방 용량은 게임 데이터에서 읽으며 그림의 칸 수에 고정하지 않는다. 스크롤은 컨트롤 면과 게이지 트랙을 재사용한다. 전체 화면 어둡게 하기와 단순 조준점은 UI 도형으로 만들며 별도 생성 이미지로 세지 않는다.

### 6.4 상태별 표시 확정

| 상태 | 표현 | 별도 이미지 |
|---|---|---|
| 기본 | 낮은 대비의 바탕과 얇은 경계 | 공용 원본 |
| 빈 슬롯 | 바탕 유지, 아이템·수량 숨김. 장비 슬롯만 부위 기호 | 공용 원본 |
| 가리킴 | 윤곽을 밝게. 크기와 슬롯 간격 유지 | 없음 |
| 선택과 키보드 포커스 | 황동 FocusCorners 표시. 선택과 포커스가 다르면 선택 슬롯은 고정, 포커스 대상은 밝은 얇은 윤곽으로 구분 | 없음 |
| 누름 | 컨트롤 면 밝기만 약하게 낮춤. 레이아웃 이동 없음 | 없음 |
| 끄는 중 | 원래 슬롯 아이콘은 약하게, 커서에는 아이콘 표시. 선택 테두리는 격자에 남음 | 없음 |
| 드롭 가능 | 안전색 윤곽 + Confirm | 없음 |
| 드롭 불가와 행동 거부 | 위험색 윤곽 + Cancel 또는 Warning + 짧은 사유 | 없음 |
| 비활성 | 대비 낮춘 면 + 읽히는 사유. 숨기지 않음 | 없음 |
| 주의와 경고와 위험 | 해당 상태 색 + Warning 및 필요한 사유. 색만 바꾸지 않음 | 없음 |

입력 불가가 선택·포커스보다 우선하지만, 실패 사유는 반드시 읽을 수 있어야 한다. 재료 부족을 자물쇠로 표현하면 잠금 해제 기능으로 오해하므로 Warning과 부족 수량을 사용한다. 확률·희귀도·쿨다운 등 현재 데이터에 없는 상태를 장식 배지로 추가하지 않는다.

## 7 이미지 생성과 제작 프롬프트

### 7.1 실제 모델이 있는 아이템

한 번에 하나의 아이콘을 생성한다. 공통 지시 아래에 §3·§4의 해당 OBJECT 지시를 붙이고 **해당 개별 참조 PNG**를 첨부한다. 합본 시트는 스타일 설명용 보조이며, 여러 물건을 하나의 출력에 넣으라는 뜻이 아니다.

```text
Create ONE production inventory icon for Train Survival, a first-person cooperative
survival game about living aboard a constantly moving train in a gentle world affected
by mysterious starseeds.

OUTPUT: one isolated item on a genuinely transparent RGBA background, square canvas.
The attached reference is the actual game object. Preserve its identifying silhouette,
parts and painted color blocks. Simplify tiny details for readability; do not redesign it
into a generic real-world equivalent. Ignore the reference background and render lighting.

STYLE: stylized semi-cartoon, hand-painted broad color planes, chunky rounded low-poly
forms, matte surfaces, restrained edge wear. Soft upper-left form shading, no cast shadow.
Use a clear three-quarter or near-side view that best exposes the identifying features.
Keep all parts inside the frame with at least 8 percent padding, longest dimension around
80 percent of the canvas. It must remain recognizable at 32, 48 and 56 pixels.

No text, numbers, labels, borders, slots, key bindings, badges, hands, characters, scenery,
floor, floating particles, external glow, watermark, checkerboard or baked background.
No photographic materials, glossy highlights, tiny scratches or dramatic perspective.
Deliver one coherent item silhouette, not a contact sheet or multiple alternative designs.

OBJECT: [paste the matching object description from the brief]
```

예: `I_Item_Revolver`에는 `Ref_Revolver.png`, `I_Res_Wood`에는 `Ref_Wood.png`를 첨부한다. **`I_Item_Harpoon`에는 발사기와 집게발을 조립한 `Ref_Harpoon.png` 한 장을 첨부한다.** 두 부품을 하나의 도구로 유지하며, 분리도·발사 중 장면·발사기 단독 그림으로 바꾸지 않는다.

### 7.2 실물 모델이 없는 아이템

§7.1의 실제 모델 보존 문단을 아래 문단으로 바꾼다. 나머지 출력·스타일 규칙은 같다. 가능하면 같은 그룹에서 먼저 검수된 아이콘 한 장을 스타일 기준으로 첨부한다.

```text
This item has no final game model yet. Follow the supplied OBJECT description as its
design definition. Match the same hand-painted visual family as the approved item icons.
Use practical repaired clothing, simple reusable enamel tableware, wrapped supplies and
chunky working tools. Do not invent logos, modern plastic packaging, luxury ornament,
magical effects or extra gameplay functions. Preserve the stated distinguishing features.
```

### 7.3 상태와 행동 기호

```text
Design ONE monochrome UI glyph for Train Survival. White silhouette on transparent RGBA,
128 by 128 output, 12 pixel safe padding. Chunky stencil-like shapes with softly rounded
ends and only very slight handmade asymmetry. It must read at 24 pixels: equivalent stroke
or bridge thickness at least 2 pixels, open gaps at least 3 pixels at display size.
Use at most two semantic elements. No texture, shading, gradient, glow, text, badge frame,
background, decorative rivets or pictorial inventory rendering.
MEANING AND SHAPE: [paste one glyph row from section 5]
```

생성된 초안은 벡터로 정리해 선 굵기·구멍·중심 정렬을 통일한 뒤 PNG로 출력한다. 각 기호마다 생성 모델이 다른 선 굵기를 임의로 결정하게 두지 않는다.

### 7.4 공통 UI 표면 연구

공통 부품의 기하학적 원본은 §6.2 치수대로 직접 제작한다. 아래 프롬프트는 **PanelHeader와 ControlFace의 표면 연구**에만 사용한다. 연구용 그림을 그대로 9슬라이스 완성품으로 취급하지 않는다.

```text
A single orthographic front-facing neutral grayscale game UI surface study for
Train Survival: a restrained matte soot-dark iron plate, very broad subtle paint variation,
softly worn corners, extremely shallow edge bevel. The central region must be nearly flat
and clean for readable UI text. No perspective, cast shadow, rivets, screws, pipes, chains,
symbols, lettering, gradients suggesting strong lighting, gold trim or ornamental frame.
The design must still look clean when reduced to a 40 pixel high button.
This is a material direction study; exact geometry and slice borders will be authored separately.
```

## 8 제작 순서와 납품

### 8.1 먼저 맞출 스타일 기준 8종

`Wood`, `Timber`, `RawFood`, `Harpoon`, `Revolver`, `Bundle`, `CookedMeal`, `WinterParka`를 먼저 만든다. 기존 모델 6종과 신규 2종을 함께 맞춰야 서로 다른 스타일의 두 세트가 되지 않는다. 32·48·56px로 축소해 목재/원목, 파랑 무기/집게, 식재료/구운 식사의 구분을 먼저 확인한다.

이와 함께 `Health`, `Frostbite`, `Grab`, `Repair` 기호 4종과 SlotBase·SlotOutline·FocusCorners·PanelBody를 조립한 한 장의 기준 화면을 만든다. 여기서 실루엣과 UI 밀도를 맞춘 뒤 나머지를 같은 규칙으로 확장한다.

### 8.2 최종 수량

**2026-10-27 스타일 기준 8종 1차 제작:** 위 아이템 8종을 512×512 투명 PNG로 제작해 §8.3의 Items 경로에 저장했다. 기존 모델 참고 6종과 신규 디자인 2종이며, 집게는 결합된 형태와 리볼버 기준의 오른쪽 위 방향을 유지했다. 내장 이미지 생성 도구로 제작했으며, 게임 UI 연결 및 Unity 실행 검증은 아직 진행하지 않았다.

- [8종 검토 시트](../../output/imagegen/ingame-ui-style-v1/StyleBaseline-8.png) — 32·48·56px 축소 비교 포함.
- [배경별 48px 판독 시트](../../output/imagegen/ingame-ui-style-v1/Readability-48px.png).
- [512px PNG 8종 다운로드](../../output/imagegen/ingame-ui-style-v1/TrainSurvival-StyleIcons-8-512.zip).
- [제작 프롬프트와 참고 자료 기록](../../output/imagegen/ingame-ui-style-v1/prompts-and-sources.json).

| 묶음 | 출력 PNG | 비고 |
|---|---:|---|
| 자원·탄약·음식 | 23 | I_Res |
| 무기·도구·장비·보따리 | 17 | I_Item |
| 상태 | 13 | G_Status |
| 행동 | 11 | G_Action |
| 시스템 조작 | 8 | G_System |
| 빈 장비 슬롯 | 4 | G_Equip |
| 공통 원본 부품 | 12 | U |
| **합계** | **88** | 상태별 색·키 글자·레시피별 복제 제외 |

실물 참고 렌더 17장과 합본 시트, UI 구성 검토 이미지는 **제작 참고 자료**이므로 위 88장에 포함하지 않는다. 집게의 원본 FBX 두 개는 조립한 참고 이미지 한 장을 만든다. 12개 UI 컴포넌트도 PNG 12장을 추가 제작한다는 뜻이 아니라 공용 원본을 조합하는 단위다.

### 8.3 파일 배치

최종 게임 이미지의 반입 경로:

```text
Assets/_Project/Art/Textures/UI/InGame/
  Items/Resources/I_Res_Wood.png                 # 23
  Items/Equipment/I_Item_Harpoon.png            # 17, 무기·도구 포함
  Glyphs/Status/G_Status_Health.png             # 13
  Glyphs/Actions/G_Action_Repair.png            # 11
  Glyphs/System/G_System_Warning.png            # 8
  Glyphs/Equipment/G_Equip_Head.png             # 4
  Components/U_SlotBase.png                    # 12
```

제작 원본·참고 렌더·검토 시트는 `docs/design/references/ingame-ui/` 또는 별도 아트 원본 보관소에 둔다. 게임에 쓰지 않는 시트를 Assets 안에 넣지 않는다. 모든 최종 PNG는 한 파일에 하나의 자산, 중앙 피벗, 일관된 투명 여백으로 출력한다.

UI 반입 기본값은 Sprite, Clamp, Bilinear, mipmap 없음이다. 아이템 512·기호128·공통 원본의 최대 크기를 각각 보존하고, 아틀라스 압축 후 24~32px 판독을 다시 확인한다. 9슬라이스 경계와 런타임 표시색은 별도의 메타데이터로 관리한다. 생성한 글자·수량을 그림에 포함하지 않는다.

### 8.4 전체 88종 1차 제작 결과 — 2026-10-27

기존 스타일 기준 8종을 보존하고 **아이템 32종, 단색 기호 36종, 공통 부품 12종을 추가 제작**했다. 최종 PNG는 아이템 40종 + 기호 36종 + 공통 부품 12종 = **88종**이다. §8.3의 게임 에셋 경로에 각각 저장했으며 Sprite 설정, 투명도 설정, 공통 부품의 9슬라이스 경계를 함께 기록했다.

- 아이템은 내장 이미지 생성 도구로 한 번에 하나씩 제작했다. 기존 목재 아이콘을 공통 화풍 기준으로 사용했고, 실물이 있는 항목은 해당 모델 렌더를 함께 첨부했다. 의류는 기존 파카, 신규 무기·도구는 기존 리볼버의 표현을 추가 참고했다.
- 기호와 공통 부품은 SVG로 직접 형태를 작성하고 규격 PNG로 출력했다. 기호는 흰색, 공통 부품은 흰색 또는 중립 회색의 색상 적용용 원본이다. SVG 원본 48개를 별도로 보관한다.
- 파일 수량·크기·알파, 기호의 단색 여부, 투명해야 할 중앙 영역, 불투명 바탕의 중앙, 패널·슬롯·버튼·게이지의 크기 변경 시 모서리 보존을 확인했다. 기존 스타일 기준 PNG 8개의 파일 내용은 변경하지 않았다.
- 아이템 32·48·56px, 기호 24·32px 검토 시트를 확인했다. **Unity 실행·아틀라스 압축 후 검증과 실제 UI 연결은 아직 진행하지 않았다.** 아래 조합 화면의 재료·수량·게이지 값은 배치 검토용 예시다.

| 산출물 | 파일 |
|---|---|
| 전체 88종 PNG·Sprite 설정·벡터 원본·검토 시트 묶음 | [전체 다운로드](../../output/imagegen/ingame-ui-production-v1/TrainSurvival-InGameUI-88.zip) |
| 추가 자원·탄약·음식 19종 | [검토 시트](../../output/imagegen/ingame-ui-production-v1/Resources-Remaining.png) |
| 추가 무기·도구·장비 13종 | [검토 시트](../../output/imagegen/ingame-ui-production-v1/Equipment-Remaining.png) |
| 단색 기호 36종 | [검토 시트](../../output/imagegen/ingame-ui-production-v1/Glyphs-36.png) |
| 공통 원본 부품 12종 | [검토 시트](../../output/imagegen/ingame-ui-production-v1/Components-12.png) |
| 최종 PNG를 사용한 UI 조합 | [조합 검토 화면](../../output/imagegen/ingame-ui-production-v1/UI-Composition.png) |
| 아이템 40종의 배경별 48px 비교 | [판독 시트](../../output/imagegen/ingame-ui-production-v1/Items-40-Readability.png) |
| 추가 아이템의 정확한 프롬프트·참고 이미지·원본 경로 | [제작 기록](../../output/imagegen/ingame-ui-production-v1/prompts-and-sources.json) |
| 벡터 원본·출력 규격·저장 경로 | [벡터 목록](../../output/imagegen/ingame-ui-production-v1/vector-manifest.json) |
| 최종 파일 해시·크기·GUID·모서리 검사 | [검증 기록](../../output/imagegen/ingame-ui-production-v1/validation.json) |

### 8.5 프로젝트 적용 — 2026-10-27

현재 게임의 IMGUI 화면에 직접 연결했다. 기존 서비스 조회, 서버 요청, 이벤트 구독을 유지하며 표시 계층을 교체했다.

- **공용 참조:** `Assets/_Project/Resources/InGameUiArt.asset`에 아이템 40종·기호 36종·공통 부품 12종의 Sprite 참조를 등록했다. 런타임은 `InGameUiArt`로 읽으며 에디터 전용 파일 검색에 의존하지 않는다. 로딩 단계에서 이미지와 실제 글자 크기를 미리 준비한다.
- **아이템:** 우하단 72px 핫바, 64px 인벤토리, 장비 4부위, 공유 창고, 보따리, 드래그 이미지, 제작 결과·재료에 적용했다. 선택 모서리, 자원 수량, 키 배지, 집게 우상단 등급, 빈 장비 부위와 장착 체크를 분리했다. 보따리 내부 보관 번호는 수량으로 노출하지 않는다. 집게의 결합 형태와 원본 방향은 변경하지 않았다.
- **상태·행동:** 체력·허기·연료·체온·버프·탄약, 동상, 낚시, 보스 체력, 사건 배너, 상호작용·거부 안내에 연결했다. 진행값이 있는 철거에는 가로 게이지를 사용한다. 긴 행동 안내는 높이를 늘리고 서로 겹치지 않도록 쌓는다.
- **공통 부품:** 패널·헤더·구분선·슬롯·선택 모서리·키캡·배지·버튼·게이지를 함께 사용한다. 게이지는 전체 크기의 채움을 잘라 비율을 표시하고, 0에서는 채움을 숨긴다. 제작 창과 인벤토리는 16px 간격의 묶음으로 중앙 배치한다.
- **기존 조작:** 숫자 키, I/Tab, E, 슬롯 교환·착용·보관 이동 요청을 유지했다. 비활성 상태였던 패널 밖 버리기는 활성화하지 않았다.

검증 결과와 화면은 `output/ui-integration/`에 저장했다. [EditMode 검사](../../output/ui-integration/editmode-results.xml)는 이미지 88종의 참조, 모든 아이템 종류, 수량 처리, 모서리 정보, 기존 인벤토리·제작 로직과 글자 준비를 확인한다. [PlayMode 검사](../../output/ui-integration/playmode-results.xml)는 실제 HUD 컴포넌트의 렌더링과 슬롯 교환·장비 착용·버리기 비활성 입력 경로를 확인한다.

| 실제 HUD 렌더링 · 1920×1080 테스트 데이터 | 캡처 |
|---|---|
| 핫바와 상태 | [HUD](../../output/ui-integration/hud.png) |
| 인벤토리와 장비 | [인벤토리](../../output/ui-integration/inventory.png) |
| 제작과 인벤토리 | [제작 화면](../../output/ui-integration/crafting-inventory.png) |
| 공유 창고 | [창고](../../output/ui-integration/storage.png) |
| 보따리 | [보따리](../../output/ui-integration/bundle.png) |

캡처는 실제 UI 코드를 테스트 서비스 데이터로 실행한 결과이며, 월드 주행이나 멀티플레이 검증 화면은 아니다. uGUI 전환은 수행하지 않았다. 현재 기능에서 쓰지 않는 기호·진행 링도 공용 참조에 포함되어 있다. 일반 자원 획득 이벤트는 팀 누적 수량만 제공하므로 종류별 획득 토스트는 추가하지 않았고, 종류가 확정된 기존 낚시 획득 알림에는 생선 아이콘을 연결했다. 1440p·16:10·21:9 및 실제 네트워크 세션의 종합 검수는 별도다.

## 9 완료 판정

후속 갱신(2026-10-27): [월드 텍스처·건축물 이미지 브리프](Train-Survival-월드텍스처-건축물-이미지-브리프.md)에서 설치 가능한 건축물 9종을 추가 제작·연결했다. 기존 88종 PNG는 유지되며 공용 UI 목록은 **97종**이다. 월드 텍스처 2장을 더한 전체 이미지 제작 목록은 **99종**이고, 후속 피드백 4개 파일은 규격만 작성했다.

1. **실물 일치:** 자원 12종과 기존 무기·보따리를 해당 참고 렌더와 나란히 놓아 실루엣·도색·주요 부품이 같은가. 집게는 발사기와 집게발이 결합된 하나의 실루엣이며 어느 부품도 빠지지 않았는가.
2. **혼동 방지:** 목재/원목, 돌/광맥, 소금/얼음/희귀 금속, 유적 부품/보스 핵, 탄약3종, 밥/스튜/온차, 보따리/보존식, 사막/방한 의류가 작은 크기에서도 구분되는가.
3. **축소 판독:** 아이템32·48·56px, 기호24·32px에서 특징이 남는가. 합본 큰 그림만 보고 통과시키지 않는다.
4. **배경 대비:** 숲의 녹색, 사막의 밝은 모래, 바다의 청색, 북극의 흰색, 밤의 어두운 화면 위에서 판독되는가. 밝은 모델도 외곽이 사라지지 않는가.
5. **투명도:** 실제 알파 채널인가. 체크무늬가 픽셀에 구워지거나 흰 테두리·검은 프린지가 남지 않는가.
6. **부품 확장:** 패널 폭280·392·800, 슬롯64·72, 버튼96·240, 게이지240·520에서 모서리·선 두께가 늘어나지 않는가.
7. **진행 표시:** 게이지0·1·50·100%, 진행 링0·25·75·100%에서 끝단·빈 공간이 정확한가. 채움 길이를 줄일 때 장식을 압축하지 않는가.
8. **상태 판독:** 흑백에서도 선택 모서리, 불가 X, 성공 체크와 사유 문구로 상태가 구분되는가. 선택과 위험이 동시에 생겨도 둘 다 보이는가.
9. **텍스트 분리:** 긴 한국어, 두 자리·세 자리 수량, 집게 단계와 키 배지가 같은 슬롯에서 충돌하지 않는가. 입력 안내가 실제 조작과 같은가.
10. **전체 화면:** 1080p·1440p·16:10·21:9에서 화면 여백·패널 간격·스크롤·대상 프롬프트가 유지되고 손·무기를 가리지 않는가.

이미지 제작, 기존 화면 적용, 전체 게임 세션 검수는 구분한다. 이번 적용 범위와 확인한 실행 환경은 §8.5에 기록했다. 규격을 확장하거나 uGUI로 전환할 때에도 같은 이미지와 상태 의미를 유지한다.

## 10 근거 문서와 현재 데이터

- [세계관 컨셉](Train-Survival-세계관-컨셉.md) — 별씨·열차·승무원의 성격.
- [비주얼 UIUX 가이드](Train-Survival-비주얼-UIUX-가이드.md) §7·9·13·14 — 팔레트·정보 계층·타이포·재사용 원칙.
- [이미지 생성 브리프](Train-Survival-이미지생성-브리프.md) §J — 실제 자원 외형의 최초 제작 정의. 이 UI 브리프에는 3D 입력용 회색 배경 규칙을 적용하지 않는다.
- [UI 구현 계획](../plans/features/UI-구현-계획.md) — 인게임 화면과 공통 컴포넌트의 사용처. 과거 아이콘 수량은 이 문서의 현재 범위와 구분한다.
- [ResourceType](../../Assets/_Project/Scripts/Gameplay/Inventory/ResourceType.cs), [HotbarItemType](../../Assets/_Project/Scripts/Gameplay/Inventory/HotbarItemType.cs), [EquipSlot](../../Assets/_Project/Scripts/Gameplay/Inventory/EquipSlot.cs) — 각각23·17·4종의 식별자.
- [FoodCatalog](../../Assets/_Project/Scripts/Gameplay/Inventory/FoodCatalog.cs), [CoreLoopHud](../../Assets/_Project/Scripts/UI/CoreLoopHud.cs), [InventoryHud](../../Assets/_Project/Scripts/UI/InventoryHud.cs), [FishingHud](../../Assets/_Project/Scripts/UI/FishingHud.cs) — 버프2종과 현재 표시·행동.
- [UiPalette](../../Assets/_Project/Scripts/UI/UiPalette.cs), [HudLayout](../../Assets/_Project/Scripts/UI/HudLayout.cs) — 유지할 색 토큰과 현재 레이아웃 참고. 새 치수는 §6의 제작 목표를 따른다.
