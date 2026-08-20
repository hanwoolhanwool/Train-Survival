# 폰트 출처·라이선스

| 파일 | 서체 | 라이선스 | 출처 |
|---|---|---|---|
| `NotoSansKR-VF.ttf` | Noto Sans KR (Variable) | **SIL Open Font License 1.1** | Google Noto — Windows 11 동봉본(`C:\Windows\Fonts`)을 복사 |

## 왜 이 서체인가

로비 메뉴 문구(`게임 시작`·`업적`·`설정`·`종료`)를 TMP로 얹으려면 한글이 전부 들어 있고
**게임에 임베딩·재배포할 수 있는** 서체가 필요하다. OFL 1.1은 둘 다 허용한다.
결정 경위는 [로비·메인 메뉴 구현 계획](../../../../docs/plans/features/로비-메인메뉴-구현-계획.md) §13 ⑲.

## 배포 전에 해야 할 일

- [ ] **OFL 1.1 전문(`OFL.txt`)을 이 폴더에 함께 둔다.** OFL은 서체를 재배포할 때
      라이선스 사본을 동봉하도록 요구한다. 현재 이 저장소에는 전문이 없다.
- [ ] 서체를 바꾸기로 하면 `F_NotoSansKR_SDF.asset` 참조 한 곳만 갈아끼우면 된다.

## 유니티 쪽 제약

가변 폰트지만 **유니티가 face로 노출하는 것은 Thin 웨이트 하나뿐**이고 가변 축(`wght`)을
지정하는 API가 없다. 그래서 굵기는 `F_NotoSansKR_SDF_Outline.mat`의 `_FaceDilate`(0.34)로 냈다.
정식 굵기가 필요하면 **정적 Bold 파일**을 따로 받아 폰트 에셋을 다시 만든다.
