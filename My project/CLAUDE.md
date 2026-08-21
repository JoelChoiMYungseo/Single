# CLAUDE.md

이 파일은 Claude Code(claude.ai/code)가 이 레포지토리에서 작업할 때 참고하는 가이드이다.

## 프로젝트 개요

**Camp Lantern** — Unity 6000.3.10f1(Unity 6) 기반 VR 프로젝트. 현재는 신규 프로젝트 초기 상태로, Oculus 샘플 에셋과 플레이스홀더 스크립트(`Assets/Scripts/Test.cs`) 외에 실제 게임 코드는 아직 없다.

기획서(GDD)는 추후 전달 예정. GDD와 실제 아키텍처가 들어오면 이 섹션과 아래 `불변 제약`·`아키텍처` 관련 내용을 그 시점에 채운다. 지금은 스텁 상태를 유지한다.

## 불변 제약 (Inviolable Constraints)

프로젝트 루트의 [`RULES.md`](./RULES.md)에 시스템을 실제로 망가뜨리는 제약이 정리되어 있다 (에디터 멈춤, GUID 손상, 빌드 실패 등). 작업 시작 전 반드시 스캔할 것.

- **RULE-01**: Domain Reload 트리거 금지 (`[InitializeOnLoad]`, `autoReferenced: true`)
- **RULE-02**: Unity 에셋 파일 직접 편집 금지 (`.meta`/`.prefab`/`.unity`/`.asset`)
- **RULE-03**: 물리 API는 `FixedUpdate`에서만
- **RULE-04**: `ProjectSettings/`는 Claude가 직접 수정하지 않음

프로젝트 고유 시스템(저장 데이터, 플랫폼 분기 등)이 생기면 해당 시점에 새 RULE을 추가한다.

## 추가 규칙 및 컨벤션

프로젝트 지식은 `.claude/` 하위에 계층별로 정리되어 있다. 필요에 따라 참조할 것:

### `.claude/rules/` — 경로 기반 코딩 규칙 (자동 로드)
- `scripts.md` — 컴포넌트 캐싱, 비동기 CancellationToken, 이벤트 구독 해제, Awake 초기화 등 범용 스크립팅 컨벤션. 프로젝트 고유 컨벤션(싱글톤 체계, UI 프레임워크 등)이 정해지면 이 파일에 추가한다.

### `.claude/domain/` — 이 프로젝트 고유 설계 의도
시스템을 수정하기 **전에** 관련 기획서를 읽어 "왜 이렇게 설계됐는가"를 파악할 것. 현재는 GDD가 없어 비어 있음.

**GDD 라우팅은 `.claude/INDEX.md` Level 2를 통해 한다.** 특정 파일명을 직접 열지 말고, INDEX.md의 keywords와 작업 주제를 매칭해 해당 GDD만 선별 로드한다.
- `gdd/` — 기획서 도착 시 여기에 추가하고 INDEX.md Level 2에 등록.
- (도메인 파일은 필요 시 생성 — `domain/README.md` 참조)

### `.claude/knowledge/` — 범용 Unity/C# 레퍼런스
언어·엔진 베스트 프랙티스가 헷갈릴 때 참조:
- `RULES.md` — 21개 범용 코딩 원칙 (R1-R21)
- `csharp-dotnet.md` — 값 타입, 박싱, 이벤트, async, LINQ
- `unity-scripting-gotchas.md` — 직렬화 함정, 코루틴 주의점, IL2CPP, Unity 2022→6000 API 리네임
- `unity-mobile-performance.md` — 모바일/XR 성능 규칙 (프로파일링, GC, 배칭, UI 등)
- `debugging/` — 디버깅 원칙 10개 (가정 의심, 버그 분류, 증상 vs 원인, 버그 일지, 정적 분석, 단언, 이분 탐색 등)
- `qa/` — QA 원칙 10개

### `.claude/INDEX.md`
위 모든 계층에 대한 키워드 기반 라우팅. **GDD를 포함한 모든 도메인 문서는 이 파일을 통해 라우팅된다.** 어떤 파일을 열어야 할지 모를 때 참고.

## 빌드 커맨드

아직 커스텀 빌드 파이프라인이 없다. Unity 에디터의 기본 `File > Build Settings`를 사용한다. 빌드 자동화 스크립트가 추가되면 여기에 기록한다.

## 아키텍처

아직 정해진 아키텍처가 없다 (신규 프로젝트, `Assets/Scripts/Test.cs` 플레이스홀더만 존재). GDD와 실제 시스템(씬 구성, 매니저 구조, 데이터 모델, 네트워킹 등)이 확정되면 이 섹션에 채운다.

## 코드 컨벤션

- **한글 주석** 허용.
- 그 외 컨벤션(싱글톤 사용 여부, 비동기 라이브러리 선택, 이벤트 시스템 등)은 실제 코드가 쌓이면서 정해지는 대로 이 섹션과 `.claude/rules/scripts.md`에 기록한다.

## 주요 패키지

프로젝트 초기 상태의 `Packages/manifest.json`을 참고. Meta(Oculus) XR 관련 샘플 에셋이 `Assets/Oculus/`에 포함되어 있다.

## 플랫폼 노트

- 타겟 플랫폼 미확정. Oculus(Meta Quest) 샘플이 포함되어 있어 VR/XR 프로젝트로 시작하는 것으로 보이나, 실제 타겟(Quest 단독인지 PICO/SteamVR도 포함하는지)은 GDD 확정 후 여기에 기록한다.
