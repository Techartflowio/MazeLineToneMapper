# MazeLine Tone Mapping

![MazeLine Logo](/Assets/Mazeline/Scripts/Editor/images/ml_logo.png)

## 개요

MazeLine Tone Mapping은 Unity Universal Render Pipeline (URP)을 위한 고급 톤 매핑 솔루션입니다. 다양한 톤 매핑 커브와 세밀한 파라미터 제어를 통해 고품질의 시각적 결과를 제공합니다.

## 작성자 정보

**개발자**: Techartflowio  
**회사**: MazeLine  
**웹사이트**: [mazeline.tech](http://mazeline.tech/)

## 개발 목적

이 프로젝트는 다음과 같은 목적으로 개발되었습니다:

- **고품질 렌더링**: 영화급 톤 매핑 기술을 게임 개발에 적용
- **유연한 제어**: 다양한 톤 매핑 알고리즘과 세밀한 파라미터 조정 지원
- **캐릭터 특화**: 캐릭터 픽셀에 대한 독립적인 톤 매핑 처리
- **성능 최적화**: Unity URP와 완벽 호환되는 효율적인 구현

## 시스템 요구사항

- **Unity 버전**: Unity 6000.2.6f1 이상
- **렌더 파이프라인**: Universal Render Pipeline (URP) 17.2.0 이상
- **그래픽스 API**: DirectX 11/12, Vulkan, Metal, OpenGL ES 3.0 이상
- **셰이더 모델**: 3.0 이상

## 현재 버전

**Version**: 1.0.0

## 사용 방안

### 1. 설치

1. 프로젝트를 Unity에서 열거나 패키지를 임포트합니다.
2. URP Renderer Asset에 MLToneMappingFeature를 추가합니다.
3. Volume 컴포넌트를 사용하여 톤 매핑 설정을 적용합니다.

### 2. 기본 설정

```csharp
// Volume Profile에서 MLToneMap 컴포넌트 추가
var volume = gameObject.AddComponent<Volume>();
var profile = ScriptableObject.CreateInstance<VolumeProfile>();
var toneMapping = profile.Add<MLToneMap>();

// 톤 매핑 타입 설정
toneMapping.ToneMapType.value = ToneMapCurveType.Filmic;
toneMapping.ToneMapType.overrideState = true;

// 노출 설정
toneMapping.Exposure.value = 1.2f;
toneMapping.Exposure.overrideState = true;
```

### 3. 고급 설정

#### 캐릭터 픽셀 제어
```csharp
// 캐릭터 픽셀을 톤 매핑에서 제외
toneMapping.IgnoreCharacterPixels.value = true;
toneMapping.IgnoreCharacterPixels.overrideState = true;

// 캐릭터 픽셀에 대한 톤 매핑 강도 조절
toneMapping.CharacterPixelsToneMapStrength.value = 0.5f;
toneMapping.CharacterPixelsToneMapStrength.overrideState = true;
```

#### AGX 톤 매핑 설정
```csharp
// AGX 전용 파라미터
toneMapping.ToneMapType.value = ToneMapCurveType.AGX;
toneMapping.AgxGamma.value = 0.8f;
toneMapping.AgxGammaPivot.value = 0.7f;
```

## API 설명

### MLToneMappingFeature

메인 렌더 피처 클래스로, URP 렌더러에 톤 매핑 패스를 추가합니다.

#### 주요 속성
- `injectionPoint`: 렌더 패스 실행 시점 (기본값: AfterRenderingPostProcessing)

#### 주요 메서드
- `Create()`: 렌더 패스 초기화
- `AddRenderPasses()`: 렌더러에 톤 매핑 패스 추가

### MLToneMap (Volume Component)

톤 매핑 설정을 제어하는 Volume 컴포넌트입니다.

#### 파라미터

| 파라미터 | 타입 | 범위 | 설명 |
|---------|------|------|------|
| `ToneMapType` | ToneMapCurveType | - | 톤 매핑 커브 타입 |
| `Exposure` | ClampedFloatParameter | 0.2 - 7.0 | 노출 값 (Stops) |
| `IgnoreCharacterPixels` | BoolParameter | - | 캐릭터 픽셀 무시 여부 |
| `CharacterPixelsToneMapStrength` | ClampedFloatParameter | 0.0 - 1.0 | 캐릭터 픽셀 톤 매핑 강도 |
| `AgxGamma` | ClampedFloatParameter | 0.0 - 1.0 | AGX 감마 값 |
| `AgxGammaPivot` | ClampedFloatParameter | 0.01 - 1.0 | AGX 감마 피벗 |

### ToneMapCurveType

지원되는 톤 매핑 커브 타입입니다.

```csharp
public enum ToneMapCurveType : int
{
    None = 0,           // 톤 매핑 비활성화
    Filmic = 1,         // 영화적 톤 매핑
    KhronosNeutral = 2, // Khronos PBR 중립적 톤 매핑
    GranTurismo = 3,    // Gran Turismo 스타일 톤 매핑
    AGX = 4            // AGX 톤 매핑
}
```

### MLToneMappingPass

실제 톤 매핑 처리를 수행하는 렌더 패스입니다.

#### 주요 기능
- Render Graph 기반 구현
- 다중 톤 매핑 알고리즘 지원
- 캐릭터 픽셀 마스킹
- 셰이더 키워드 기반 최적화

## 사용 예제

### 기본 사용법

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using ML;

public class ToneMappingController : MonoBehaviour
{
    [SerializeField] private Volume volume;
    private MLToneMap toneMapping;

    void Start()
    {
        // Volume Profile에서 MLToneMap 컴포넌트 가져오기
        if (volume.profile.TryGet<MLToneMap>(out toneMapping))
        {
            // 초기 설정
            SetupToneMapping();
        }
    }

    void SetupToneMapping()
    {
        // Filmic 톤 매핑 적용
        toneMapping.ToneMapType.value = ToneMapCurveType.Filmic;
        toneMapping.ToneMapType.overrideState = true;

        // 노출 조정
        toneMapping.Exposure.value = 1.5f;
        toneMapping.Exposure.overrideState = true;

        // 캐릭터 픽셀 보호
        toneMapping.IgnoreCharacterPixels.value = true;
        toneMapping.IgnoreCharacterPixels.overrideState = true;
    }

    public void ToggleToneMappingType()
    {
        // 톤 매핑 타입 순환
        var currentType = toneMapping.ToneMapType.value;
        var nextType = (ToneMapCurveType)(((int)currentType + 1) % 5);
        toneMapping.ToneMapType.value = nextType;
    }
}
```

### 동적 톤 매핑 조정

```csharp
public class DynamicToneMapping : MonoBehaviour
{
    [SerializeField] private Volume volume;
    [SerializeField] private AnimationCurve exposureCurve;
    [SerializeField] private float transitionDuration = 2f;

    private MLToneMap toneMapping;
    private Coroutine transitionCoroutine;

    void Start()
    {
        volume.profile.TryGet<MLToneMap>(out toneMapping);
    }

    public void TransitionToNightMode()
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(TransitionExposure(0.5f));
    }

    public void TransitionToDayMode()
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(TransitionExposure(1.2f));
    }

    private IEnumerator TransitionExposure(float targetExposure)
    {
        float startExposure = toneMapping.Exposure.value;
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t = elapsedTime / transitionDuration;
            float curveValue = exposureCurve.Evaluate(t);
            
            toneMapping.Exposure.value = Mathf.Lerp(startExposure, targetExposure, curveValue);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        toneMapping.Exposure.value = targetExposure;
        transitionCoroutine = null;
    }
}
```

## 성능 고려사항

- **셰이더 변형**: 각 톤 매핑 타입은 별도의 셰이더 키워드를 사용하여 최적화됩니다.
- **메모리 사용량**: 추가 컬러 버퍼 복사가 필요합니다.
- **GPU 성능**: 전체 화면 효과로 픽셀 처리량에 비례한 성능 영향이 있습니다.

## 문제 해결

### 일반적인 문제

1. **톤 매핑이 적용되지 않음**
   - URP Renderer Asset에 MLToneMappingFeature가 추가되었는지 확인
   - Volume의 Override State가 활성화되었는지 확인

2. **성능 저하**
   - 불필요한 톤 매핑 타입은 None으로 설정
   - 여러 Volume이 중복 적용되지 않도록 확인

3. **예상과 다른 결과**
   - 색공간 설정 확인 (Linear/Gamma)
   - 후처리 순서 확인

## 기여하기

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 라이센스

이 프로젝트는 MIT 라이센스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

```
MIT License

Copyright (c) 2025 Techartflowio

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 연락처

- **웹사이트**: [mazeline.tech](http://mazeline.tech/)
- **이메일**: contact@mazeline.tech
- **GitHub**: [MazeLine GitHub](https://github.com/techartflowio)

## 변경 로그

### v1.0.0 (2025-01-XX)
- 초기 릴리스
- 5가지 톤 매핑 커브 지원
- 캐릭터 픽셀 마스킹 기능
- URP Render Graph 호환성
- AGX 톤 매핑 지원

---

*MazeLine - 게임 개발을 위한 전문 렌더링 솔루션*
