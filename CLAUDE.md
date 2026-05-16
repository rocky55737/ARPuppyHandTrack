# ARPuppyHandTrack — Claude Code Guidelines

## Project Context
- Unity AR project with hand tracking
- Language: C#
- Unity Version: 6000.0.75f1
- Target Platform: Android

## Karpathy Guidelines

### 1. Think Before Coding
- State assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them.
- If simpler approach exists, say so.

### 2. Simplicity First
- Minimum code that solves the problem.
- No features beyond what was asked.
- No speculative abstractions.
- If 200 lines could be 50, rewrite it.

### 3. Surgical Changes
- Don't touch adjacent code, comments, or formatting.
- Match existing Unity/C# style conventions.
- If you notice unrelated issues, mention them — don't fix them.

### 4. Goal-Driven Execution
- Transform tasks into verifiable goals.
- For multi-step tasks, state a brief plan first.

## Unity-Specific Rules
- MonoBehaviour 생명주기(Awake/Start/Update) 순서 지킬 것
- 새 패키지 추가 시 반드시 먼저 언급할 것
- Assets/ 폴더 구조 임의로 변경 금지
- SerializeField는 private 유지

## Language
- 한국어로 소통