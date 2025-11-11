# MAZELINE Tone Mapping for HDRP

URP용 MazeLine ToneMapper를 HDRP의 Render Graph 기반으로 변환한 솔루션입니다.

## 개요

이 패키지는 HDRP에서 고급 톤매핑 기능을 제공합니다:

- **다양한 톤매핑 알고리즘**: Filmic, Neutral, Gran Turismo, AGX
- **레이어 기반 마스킹**: 캐릭터와 FX 효과별 독립적 톤매핑 제어
- **Render Graph 최적화**: HDRP의 최신 렌더링 파이프라인과 완벽 호환

## 파일 구조

```
Assets/SGE/Scripts/Runtime/ToneMapping/HDRP/
├── SGToneMappingHDRP.cs               # Volume Component (메인)
├── SGToneMappingCustomPostProcess.cs  # CustomPostProcess 구현
├── SGLayerMaskCustomPass.cs           # CustomPass (레이어 마스크)
├── HDRPToneMappingShader.shader       # 메인 톤매핑 셰이더
├── SGToneMappingHDRPEditor.cs         # 에디터 UI
├── ML.ToneMapping.HDRP.asmdef         # Assembly Definition
└── README_HDRP.md                     # 이 파일
```

## 시스템 요구사항

- **Unity**: 2021.3 이상
- **HDRP**: 13.x 이상
- **Render Graph**: 활성화 필수

## 설치

### 1. HDRP 에셋 설정

프로젝트의 HDRP 에셋에서 다음을 확인하세요:

1. **Window > Rendering > HDRP Asset**를 열기
2. **Rendering** 섹션에서:
   - **Enable Custom Pass** ✓
   - **Render Graph** ✓ (기본값: 활성화)
3. **Post-processing** 섹션 확인

### 2. Volume 설정

Scene에서 톤매핑을 적용하려면:

```
1. Hierarchy에서 GameObject 생성
2. "Global Volume" 컴포넌트 추가 (또는 기존 Global Volume 사용)
3. Volume Profile 할당 (없으면 새로 생성)
4. "Add Override" > "Post-processing" > "MAZELINE Tone Mapping (HDRP)"
```

### 3. 파라미터 설정

Volume Component에서 다음을 조정:

| 파라미터 | 범위 | 설명 |
|---------|------|------|
| Tone Mapping Type | Enum | 톤매핑 알고리즘 선택 (None/Filmic/Neutral/GranTurismo/AGX) |
| Exposure | 0.2 - 7.0 | 노출 보정 |
| AGX Gamma | 0.0 - 1.0 | AGX 감마 보정 (AGX 타입만) |
| AGX Gamma Pivot | 0.01 - 1.0 | AGX 감마 피벗 포인트 (AGX 타입만) |
| Character Mask Weight | 0.1 - 1.0 | 캐릭터 레이어 톤매핑 가중치 |
| FX Mask Weight | 0.1 - 1.0 | FX 레이어 톤매핑 가중치 |

## 톤매핑 알고리즘

### None
- 톤매핑 미적용, 원본 색상 반환

### Filmic (Uncharted 2)
- 영화적 외관
- 높은 동적 범위 표현에 최적화
- **권장 Exposure**: 1.0 - 2.0

### Neutral (Khronos PBR)
- 균형잡힌 보존적 톤매핑
- 정확한 색상 표현
- **권장 Exposure**: 1.0 - 3.0

### Gran Turismo
- 고급 곡선 기반 톤매핑
- 아케이드 게임 스타일
- **권장 Exposure**: 1.0 - 2.0

### AGX (Analysis of Graphics Exposure)
- 최고 품질의 톤매핑
- 예술적 색감 표현 가능
- **권장 Exposure**: 1.0 - 1.5
- AGX Gamma, AGX Gamma Pivot으로 세밀한 조정 가능

## 레이어 마스크 사용

### 목적
특정 게임 요소(캐릭터, 이펙트 등)에만 선택적으로 톤매핑 적용

### 설정 방법

1. **CustomPass Volume 생성**:
   ```
   GameObject 생성 > "Custom Pass Volume" 컴포넌트 추가
   ```

2. **CustomPass 추가**:
   ```
   "Add Pass" > "MAZELINE Layer Mask Pass"
   ```

3. **레이어 마스크 설정**:
   - Character Layers: 캐릭터가 속한 레이어 선택
   - FX Layers: 이펙트가 속한 레이어 선택

4. **Tone Mapping Volume에서 가중치 조정**:
   - Character Mask Weight: 캐릭터에 대한 톤매핑 강도
   - FX Mask Weight: 이펙트에 대한 톤매핑 강도

### 예시

```
배경: 톤매핑 완전 적용 (Weight = 1.0)
캐릭터: 톤매핑 부분 적용 (Character Weight = 0.7)
이펙트: 톤매핑 최소 적용 (FX Weight = 0.3)
```

## Render Graph 구현

### 주요 특징

1. **자동 리소스 관리**: GPU 메모리 자동 할당/해제
2. **동적 해상도 지원**: 화면 해상도 변경 시 자동 대응
3. **렌더링 최적화**: 불필요한 렌더링 패스 제거
4. **멀티스레드 안전**: 병렬 렌더링 지원

### RecordRenderGraph 호출 흐름

```
RecordRenderGraph (HDRP Framework)
  ├─ CreateRenderGraphTexture (임시 텍스처 생성)
  ├─ AddRasterRenderPass (렌더 패스 추가)
  ├─ SetRenderAttachment (출력 텍스처 설정)
  ├─ UseTexture (의존성 설정)
  └─ SetRenderFunc (실제 렌더링 함수)
```

## 성능 최적화

### 권장사항

1. **불필요한 마스크 비활성화**:
   - 캐릭터/이펙트 마스크가 필요 없으면 끄기
   - 렌더 타겟 생성 비용 감소

2. **톤매핑 알고리즘 선택**:
   - **가장 빠름**: Neutral, Filmic
   - **중간 성능**: Gran Turismo
   - **가장 느림**: AGX (품질 우선)

3. **마스크 해상도**:
   - R8 포맷으로 메모리 절약 (1 바이트/픽셀)
   - 필요시 다운스케일 가능

### 성능 측정

HDRP의 프레임 디버거(FrameDebugger)에서:
1. "MAZELINE Tone Mapping" 패스 확인
2. GPU 메모리 사용량 모니터링
3. 렌더링 시간 측정

## 문제 해결

### 톤매핑이 적용되지 않음

1. **Volume Component 확인**:
   - Tone Mapping Type이 None이 아닌지 확인
   - Override State가 활성화되어 있는지 확인

2. **HDRP 설정 확인**:
   - HDRP 에셋에서 Custom Post Process 활성화 확인
   - Render Graph 활성화 확인

3. **셰이더 로드 실패**:
   ```csharp
   Debug.Log(Shader.Find("Hidden/MAZELINE/PostProcess/ToneMapping"));
   // null이 아니어야 함
   ```

### 성능 저하

1. **프레임 디버거 확인**:
   - "MAZELINE Tone Mapping" 패스의 GPU 시간 확인
   - 다른 패스와의 상호작용 확인

2. **마스크 비활성화 테스트**:
   - Character/FX 마스크 비활성화 후 성능 측정
   - 마스크 렌더링이 병목인지 확인

3. **알고리즘 변경**:
   - AGX → Neutral로 변경 후 성능 비교

### 색상이 이상함

1. **컬러 스페이스 확인**:
   - Project Settings > Player > Other Settings > Color Space
   - Linear 추천

2. **Exposure 값 조정**:
   - 각 톤매핑 타입별 권장값 참고

3. **마스크 가중치 확인**:
   - Character/FX Mask Weight 값 확인
   - 필요시 0.5로 시작하여 조정

## 고급 사용법

### 런타임 파라미터 변경

```csharp
// Volume 컴포넌트 가져오기
var volumeStack = VolumeManager.instance.stack;
var toneMapping = volumeStack.GetComponent<SGToneMappingHDRP>();

// 파라미터 변경
toneMapping.ToneMapType.value = ToneMapCurveType.AGX;
toneMapping.Exposure.value = 1.5f;
toneMapping.LayerMaskApplyWeight.value = 0.8f;

// 즉시 적용됨
```

### CustomPass 직접 제어

```csharp
// CustomPass 찾기
var customPassVolume = GetComponent<CustomPassVolume>();
var layerMaskPass = customPassVolume.customPasses[0] as SGLayerMaskCustomPass;

// 레이어 마스크 변경
layerMaskPass.SetCharacterLayerMask(LayerMask.GetMask("Character"));
```

## HDRP vs URP 비교

| 항목 | URP | HDRP |
|------|-----|------|
| 렌더링 파이프라인 | ScriptableRenderPass | CustomPostProcess/CustomPass |
| 마스크 렌더링 | ScriptableRenderPass | CustomPass |
| 주입 포인트 | AfterRenderingPostProcessing | AfterPostProcess |
| 리소스 관리 | RTHandle | RTHandles (Render Graph) |
| 셰이더 경로 | URP ShaderLibrary | HDRP ShaderLibrary |

## 호환성

### HDRP 버전
- ✓ HDRP 13.x (2021.3+)
- ✓ HDRP 14.x (2022.1+)
- ✓ HDRP 15.x (2022.2+)
- ✓ HDRP 16.x (2023.x+)

### Render Pipeline
- ✗ Built-in Render Pipeline
- ✗ Universal Render Pipeline (URP용 별도 패키지 사용)
- ✓ HDRP (이 패키지)

## 라이선스 및 크레딧

MAZELINE Corporation의 MazeLine Tone Mapping 기술을 HDRP에 포팅한 버전입니다.

## 추가 도움

문제가 발생하면:
1. 이 README의 "문제 해결" 섹션 참고
2. HDRP 공식 문서: https://docs.unity3d.com/Manual/render-pipelines-high-definition.html
3. Render Graph 가이드: https://docs.unity3d.com/Manual/RenderGraph.html

---

**마지막 업데이트**: 2024년 11월
**버전**: 1.0.0 (HDRP)
