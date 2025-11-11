# SGE Tone Mapping System for HDRP

Unity HDRP (High Definition Render Pipeline)용 MazeLine 톤매핑 시스템입니다.

## 개요

이 시스템은 URP에서 HDRP로 포팅된 커스텀 톤매핑 솔루션으로, 다양한 톤매핑 커브와 레이어 마스크 기반 선택적 적용을 지원합니다.

## 주요 기능

- **다양한 톤매핑 커브 지원**
  - Filmic (Uncharted 2)
  - Khronos PBR Neutral
  - Gran Turismo
  - AGX (Academy Color Encoding System inspired)

- **레이어 마스크 기반 선택적 적용**
  - 캐릭터 레이어 마스크
  - FX/파티클 레이어 마스크
  - 각 마스크별 독립적인 가중치 조절

## 파일 구조

```
Assets/SGE/Scripts/Runtime/ToneMapping/HDRP/
├── SGToneMappingHDRP.cs           # 메인 CustomPostProcess 컴포넌트
├── SGLayerMaskPass.cs              # CustomPass for 레이어 마스크 렌더링
├── Shaders/
│   ├── HDRPToneMappingShader.shader      # 톤매핑 셰이더
│   ├── HDRPCharacterMaskShader.shader    # 캐릭터 마스크 셰이더
│   ├── HDRPFXMaskShader.shader           # FX 마스크 셰이더
│   └── SGCommonHDRP.hlsl                 # 공통 유틸리티
└── Editor/
    └── SGToneMappingHDRPEditor.cs        # 에디터 UI
└── ML.ToneMapping.HDRP.asmdef            # 어셈블리 정의

```

## 설치 방법

### 1. 필수 요구사항
- Unity 2021.3 LTS 이상
- HDRP 12.0 이상
- Render Graph 활성화

### 2. HDRP Asset 설정
1. Project Settings → Quality → HDRP Asset 선택
2. Rendering → Post-processing → Custom Post Process Orders 확장
3. `After Post Process` 섹션에 `SGToneMappingHDRP` 추가

### 3. 파일 설치
1. 제공된 스크립트 파일들을 프로젝트에 추가
2. 셰이더 파일들을 프로젝트에 추가
3. 어셈블리 정의 파일 추가

### 4. Volume 설정
1. 씬에 Global Volume 생성 (또는 기존 Volume 사용)
2. Volume Profile에 `Post-processing → SGE → Tone Mapping` Override 추가
3. 원하는 톤매핑 타입 선택 및 파라미터 조정

### 5. CustomPass Volume 설정
1. 씬에 GameObject 생성
2. `Custom Pass Volume` 컴포넌트 추가
3. `Add New Custom Pass` → `SGLayerMaskPass` 선택
4. Injection Point를 `BeforeTransparent`로 설정
5. Character와 FX 레이어 마스크 설정

## 사용 방법

### Volume Component 파라미터

**Tone Map Type**
- None: 톤매핑 비활성화
- Filmic: 영화적 톤매핑
- Khronos Neutral: 중립적 PBR 톤매핑
- Gran Turismo: GT 스타일 톤매핑
- AGX: 고급 색상 그레이딩

**Common Parameters**
- Exposure: 노출 조정 (0.2 ~ 7.0)

**AGX Parameters** (AGX 선택시만)
- AGX Gamma: 미드톤 형상화 (0.0 ~ 2.0)
- AGX Gamma Pivot: 감마 기준점 (0.01 ~ 1.0)

**Layer Mask Weights**
- Character Weight: 캐릭터 레이어 톤매핑 적용도 (0 ~ 1)
- FX Weight: FX 레이어 톤매핑 적용도 (0 ~ 1)

### CustomPass 설정

**Character Layer Mask**
- 톤매핑을 선택적으로 적용할 캐릭터 레이어 선택

**FX Layer Mask**
- 톤매핑을 선택적으로 적용할 이펙트/파티클 레이어 선택

## URP와의 차이점

### 아키텍처 변경
| URP | HDRP |
|-----|------|
| ScriptableRendererFeature | CustomPass |
| ScriptableRenderPass | CustomPostProcess |
| RenderPassEvent | CustomPassInjectionPoint |

### Render Graph
- HDRP는 Render Graph가 필수
- RTHandle을 통한 동적 해상도 지원
- 더 효율적인 리소스 관리

### 셰이더 경로
- URP: `Packages/com.unity.render-pipelines.universal/`
- HDRP: `Packages/com.unity.render-pipelines.high-definition/`

## 최적화 팁

1. **레이어 마스크 최적화**
   - 필요한 경우에만 레이어 마스크 사용
   - R8 포맷 사용으로 메모리 절약
   - 마스크 해상도는 자동으로 다이나믹 스케일링됨

2. **톤매핑 타입 선택**
   - AGX: 가장 고급이지만 비용이 높음
   - Neutral: 균형잡힌 성능과 품질
   - Filmic: 영화적 룩을 원할 때
   - GT: 특정 스타일라이즈드 룩

3. **가중치 조정**
   - 캐릭터와 배경의 차별화가 필요할 때만 가중치 조정
   - 기본값 1.0은 전체 톤매핑 적용

## 트러블슈팅

**Q: 톤매핑이 적용되지 않음**
- HDRP Asset에서 Custom Post Process가 추가되었는지 확인
- Volume의 Is Global이 체크되어 있는지 확인
- CustomPass Volume이 활성화되어 있는지 확인

**Q: 레이어 마스크가 작동하지 않음**
- CustomPass의 Injection Point 확인 (BeforeTransparent 권장)
- 레이어 설정이 올바른지 확인
- 오브젝트의 레이어 할당 확인

**Q: 성능 이슈**
- 불필요한 레이어 마스크 비활성화
- 더 간단한 톤매핑 커브 사용 고려
- Dynamic Resolution 활용

**Q: Shader not found 오류**
- 셰이더 파일이 올바른 경로에 있는지 확인
- 셰이더 이름이 코드와 일치하는지 확인
- Reimport All 시도

## 마이그레이션 가이드 (URP → HDRP)

### 1. Volume Component 변경
```csharp
// URP
SGToneMappingVC → SGToneMappingHDRP
ToneMapType → toneMapType (camelCase)
Exposure → exposure
```

### 2. Feature → CustomPass
```csharp
// URP
SGToneMappingFeature → SGLayerMaskPass
RenderPassEvent → CustomPassInjectionPoint
```

### 3. 셰이더 변경
- Include 경로 변경
- HDRP 전용 매크로 사용
- `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` 추가

## API Reference

### SGToneMappingHDRP
```csharp
public class SGToneMappingHDRP : CustomPostProcessVolumeComponent
{
    public ToneMapCurveTypeParameter toneMapType;
    public ClampedFloatParameter exposure;
    public ClampedFloatParameter agxGamma;
    public ClampedFloatParameter agxGammaPivot;
    public ClampedFloatParameter layerMaskApplyWeight;
    public ClampedFloatParameter fxLayerMaskApplyWeight;
}
```

### SGLayerMaskPass
```csharp
public class SGLayerMaskPass : CustomPass
{
    public LayerMask characterLayerMask;
    public LayerMask fxLayerMask;
}
```

## 성능 프로파일링

| 톤매핑 타입 | GPU 시간 (ms) | 메모리 사용 |
|------------|--------------|------------|
| None       | 0.0          | 0 MB       |
| Filmic     | ~0.2         | 최소       |
| Neutral    | ~0.2         | 최소       |
| GT         | ~0.3         | 최소       |
| AGX        | ~0.4         | 최소       |

*테스트 환경: RTX 3070, 1920x1080 해상도

## 라이선스

© 2025 MazeLine. All rights reserved.

## 지원

기술 지원이 필요하신 경우 MazeLine 기술팀에 문의하세요.

## 변경 이력

### v1.0.0 (2025.01)
- 최초 HDRP 버전 릴리즈
- URP에서 포팅 완료
- Render Graph 지원 추가
