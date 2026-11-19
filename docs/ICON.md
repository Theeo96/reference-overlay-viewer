# 앱 아이콘

겹쳐진 두 이미지 창과 커서를 결합했다. 겹침은 레퍼런스 오버레이를, 커서는 클릭 통과 기능을 나타낸다. 청록색과 남색을 사용하고 글자와 세부 장식은 생략했다.

## 파일과 적용 위치

- `assets/icon-source.png`: 내장 image_gen 도구로 생성한 원본. 투명 배경 유지.
- `src/ReferenceOverlayViewer/Assets/app-icon.png`: 256px 표시용 PNG.
- `src/ReferenceOverlayViewer/Assets/app.ico`: 16, 20, 24, 32, 40, 48, 64, 128, 256px가 포함된 Windows 아이콘.
- csproj의 ApplicationIcon으로 exe에 포함하고 WPF Window.Icon과 상단 바 이미지에 적용한다.
- 형식 변환 재현: `powershell -NoProfile -STA -File scripts/make-icon.ps1`.

아이콘은 새로 생성했으며 개인 참고 이미지를 사용하지 않았다. 원본 생성은 내장 도구를 사용했고, ICO 포맷과 작은 크기 변환은 WPF 렌더러로 수행했다. 실제 작업표시줄 표시를 위한 Computer Use 검증은 하지 않는다.

## 적용 검증

2026-11-19 KST: Release 빌드 경고 0개 / 오류 0개. 개발 빌드와 자체 포함 배포물의 기존 자동 검사 각각 30/30 통과. ICO 디렉터리의 9개 프레임 크기를 확인했고, 배포 exe에서 실제 아이콘을 추출하여 생성한 도안인지 확인했다. 상단 바의 520 DIP 렌더링도 확인했다. Windows 탐색기·작업표시줄의 실제 표시 및 아이콘 캐시는 직접 조작하지 않았다.

## 생성 프롬프트

```text
Use case: logo-brand. Create one finished Windows desktop application icon for Reference Overlay Viewer: a utility that floats a translucent reference image over other apps and lets mouse clicks pass through. Simple bold geometric flat icon, straight-on, no perspective. Two offset overlapping rounded rectangular image/window frames; the front frame has a solid narrow top bar and a minimal mountain silhouette with one circular sun. A small clear white mouse pointer overlaps the lower right edge to suggest click-through. Deep navy and luminous cyan/teal palette with white accents, high contrast, very few large shapes, thick readable strokes. Transparent outside the mark, no enclosing square tile. Centered compact square composition with roughly 8 percent transparent padding, designed to remain legible at 16, 32 and 48 pixels. No text, no letters, no gradients, no shadows, no glow, no tiny window controls, no extra decoration, no watermark. Output a single standalone icon, not an icon sheet, mockup or presentation.
```
