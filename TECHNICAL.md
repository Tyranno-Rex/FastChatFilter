# FastChatFilter 기술 문서

## 목차
1. [개요](#1-개요)
2. [아키텍처](#2-아키텍처)
3. [핵심 데이터 구조](#3-핵심-데이터-구조)
4. [매칭 알고리즘](#4-매칭-알고리즘)
5. [바이너리 포맷](#5-바이너리-포맷)
6. [성능 최적화 기법](#6-성능-최적화-기법)
7. [API 설계](#7-api-설계)

---

## 1. 개요

FastChatFilter는 게임 서버, 채팅 시스템 등 실시간 애플리케이션을 위한 고성능 비속어 필터링 라이브러리입니다.

### 핵심 목표
- **Zero-allocation**: GC 압력 최소화
- **O(n) 시간 복잡도**: 텍스트 길이에 비례하는 검색 시간
- **False Positive 제거**: Trie + CRC32 이중 검증으로 오탐 방지

### 기술 스택
- .NET 8.0 / .NET Standard 2.1
- Span<T> 기반 API
- SSE4.2 하드웨어 가속 (CRC32C)
- Binary Trie 데이터 구조

---

## 2. 아키텍처

### 2.1 하이브리드 매칭 플로우

```
입력 문자열: "hello badword world"
       │
       ▼
┌─────────────────────┐
│   Sliding Window    │  ← 각 위치에서 시작
│   (Position 0~n)    │
└─────────────────────┘
       │
       ▼
┌─────────────────────┐
│   Trie Traversal    │  ← O(m) 탐색, m = 단어 길이
│   (부분 문자열 매칭)  │
└─────────────────────┘
       │
       ├─ 매칭 실패 → 다음 위치로 이동
       │
       └─ Terminal Node 도달
              │
              ▼
       ┌─────────────────────┐
       │   CRC32 Hash 검증   │  ← O(1) 해시 조회
       │   (Binary Search)   │
       └─────────────────────┘
              │
              ├─ 해시 불일치 → False Positive 제거
              │
              └─ 해시 일치 → 비속어 확정!
```

### 2.2 왜 하이브리드 방식인가?

| 방식 | 장점 | 단점 |
|------|------|------|
| Trie Only | 빠른 prefix 매칭 | 메모리 사용량 높음, 오탐 가능 |
| Hash Only | 메모리 효율적 | O(n*m) 복잡도, 모든 부분문자열 검사 필요 |
| **Hybrid** | 두 장점 결합 | 구현 복잡도 증가 |

**Hybrid 방식의 이점:**
1. Trie로 빠르게 후보를 찾고
2. CRC32로 정확하게 검증
3. False Positive 완전 제거

---

## 3. 핵심 데이터 구조

### 3.1 Binary Trie

Trie(트라이)는 문자열 검색에 최적화된 트리 구조입니다.

```
예: "bad", "badword", "ban" 저장

        [Root]
           │
           b
           │
          [1]
           │
           a
           │
          [2]
          / \
         d   n
        /     \
      [3]●    [6]●
       │
       w
       │
      [4]
       │
       o
       │
      [5]
       │
       r
       │
      [6]
       │
       d
       │
      [7]●

● = Terminal Node (단어 끝)
```

#### 노드 구조 (8 bytes)
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TrieNode
{
    public readonly int FirstEdgeIndex;   // 4 bytes - 첫 번째 엣지 인덱스
    public readonly ushort EdgeCount;     // 2 bytes - 자식 엣지 수
    public readonly ushort Flags;         // 2 bytes - IsTerminal 등 플래그
}
```

#### 엣지 구조 (8 bytes)
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TrieEdge
{
    public readonly char Character;       // 2 bytes - 전이 문자
    private readonly ushort _padding;     // 2 bytes - 정렬용
    public readonly int ChildNodeIndex;   // 4 bytes - 자식 노드 인덱스
}
```

#### 자식 노드 탐색 - Binary Search O(log k)
```csharp
private int FindChildNode(int nodeIndex, char c)
{
    ReadOnlySpan<TrieEdge> edges = _trie.GetEdges(nodeIndex);

    int left = 0;
    int right = edges.Length - 1;

    while (left <= right)
    {
        int mid = (left + right) >> 1;  // 비트 시프트로 나누기 2
        char midChar = edges[mid].Character;

        if (midChar == c)
            return edges[mid].ChildNodeIndex;
        else if (midChar < c)
            left = mid + 1;
        else
            right = mid - 1;
    }

    return -1;  // 찾지 못함
}
```

### 3.2 CRC32 Hash Set

CRC32 해시를 정렬된 배열에 저장하여 Binary Search로 O(log n) 조회합니다.

```csharp
public sealed class HashSet32
{
    private readonly uint[] _hashes;      // 정렬된 해시 배열
    public readonly int MinWordLength;    // 최소 단어 길이
    public readonly int MaxWordLength;    // 최대 단어 길이

    public bool Contains(uint hash)
    {
        // Binary Search - O(log n)
        int left = 0;
        int right = _hashes.Length - 1;

        while (left <= right)
        {
            int mid = (left + right) >> 1;
            uint midHash = _hashes[mid];

            if (midHash == hash)
                return true;
            else if (midHash < hash)
                left = mid + 1;
            else
                right = mid - 1;
        }

        return false;
    }
}
```

---

## 4. 매칭 알고리즘

### 4.1 HybridMatcher 핵심 로직

```csharp
public bool Contains(ReadOnlySpan<char> text)
{
    // Sliding Window - 모든 시작 위치에서 검사
    for (int startIndex = 0; startIndex < text.Length; startIndex++)
    {
        if (MatchFromPosition(text, startIndex, out _))
            return true;
    }
    return false;
}

private bool MatchFromPosition(ReadOnlySpan<char> text, int startIndex, out int matchLength)
{
    matchLength = 0;
    int currentNodeIndex = _trie.RootIndex;
    int longestVerifiedMatch = 0;

    // Trie 탐색
    for (int i = startIndex; i < text.Length; i++)
    {
        char c = text[i];

        // Binary Search로 자식 노드 찾기
        int childIndex = FindChildNode(currentNodeIndex, c);

        if (childIndex < 0)
            break;  // 더 이상 매칭되는 경로 없음

        currentNodeIndex = childIndex;
        var node = _trie.GetNode(currentNodeIndex);

        // Terminal Node 도달 - CRC32 검증
        if (node.IsTerminal)
        {
            int candidateLength = i - startIndex + 1;
            var candidate = text.Slice(startIndex, candidateLength);

            // CRC32 해시 계산 및 검증
            uint hash = Crc32.Compute(candidate);
            if (_hashSet.Contains(hash))
            {
                longestVerifiedMatch = candidateLength;
                // 더 긴 매칭을 찾기 위해 계속 탐색
            }
        }
    }

    matchLength = longestVerifiedMatch;
    return longestVerifiedMatch > 0;
}
```

### 4.2 시간 복잡도 분석

| 연산 | 복잡도 | 설명 |
|------|--------|------|
| 전체 검색 | O(n * m * log k) | n=텍스트 길이, m=최대 단어 길이, k=평균 자식 수 |
| Trie 탐색 | O(m * log k) | 단어 길이 × 자식 노드 검색 |
| CRC32 계산 | O(m) | 단어 길이에 비례 |
| 해시 조회 | O(log h) | h=총 해시 수, Binary Search |

**실제 성능**: 대부분의 경우 k가 작고 (평균 2-3), 단어가 짧아서 거의 O(n)에 가깝게 동작합니다.

---

## 5. 바이너리 포맷

### 5.1 파일 구조 (FCF3 포맷)

```
┌────────────────────────────────────────┐
│            HEADER (32 bytes)           │
├────────────────────────────────────────┤
│  Magic: "FCF3" (0x33464346)   4 bytes  │
│  Version: 3                   2 bytes  │
│  Flags: 0                     2 bytes  │
│  NodeCount                    4 bytes  │
│  EdgeCount                    4 bytes  │
│  HashCount                    4 bytes  │
│  MinWordLength                4 bytes  │
│  MaxWordLength                4 bytes  │
│  Reserved                     4 bytes  │
├────────────────────────────────────────┤
│         TRIE NODES (N * 8 bytes)       │
│  [FirstEdgeIndex, EdgeCount, Flags]    │
├────────────────────────────────────────┤
│         TRIE EDGES (E * 8 bytes)       │
│  [Character, Padding, ChildNodeIndex]  │
├────────────────────────────────────────┤
│        CRC32 HASHES (H * 4 bytes)      │
│  [Sorted uint32 hashes for bin search] │
└────────────────────────────────────────┘
```

### 5.2 BinaryHeader 구조체

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
public readonly struct BinaryHeader
{
    public const int MagicValue = 0x33464346;  // "FCF3" in little-endian
    public const ushort CurrentVersion = 3;
    public const int SizeInBytes = 32;

    public readonly int Magic;           // 4 bytes
    public readonly ushort Version;      // 2 bytes
    public readonly ushort Flags;        // 2 bytes
    public readonly int NodeCount;       // 4 bytes
    public readonly int EdgeCount;       // 4 bytes
    public readonly int HashCount;       // 4 bytes
    public readonly int MinWordLength;   // 4 bytes
    public readonly int MaxWordLength;   // 4 bytes
    private readonly int _reserved;      // 4 bytes (정렬용)
}
```

### 5.3 바이너리 로딩 (Memory-Mapped 방식)

```csharp
public static (BinaryTrie Trie, HashSet32 HashSet) LoadFromBytes(byte[] data)
{
    // 1. 헤더 읽기
    var header = MemoryMarshal.Read<BinaryHeader>(data.AsSpan(0, 32));

    // 2. 노드 배열 읽기
    int nodeOffset = BinaryHeader.SizeInBytes;
    int nodeSize = header.NodeCount * 8;
    var nodes = MemoryMarshal.Cast<byte, TrieNode>(
        data.AsSpan(nodeOffset, nodeSize)).ToArray();

    // 3. 엣지 배열 읽기
    int edgeOffset = nodeOffset + nodeSize;
    int edgeSize = header.EdgeCount * 8;
    var edges = MemoryMarshal.Cast<byte, TrieEdge>(
        data.AsSpan(edgeOffset, edgeSize)).ToArray();

    // 4. 해시 배열 읽기
    int hashOffset = edgeOffset + edgeSize;
    int hashSize = header.HashCount * 4;
    var hashes = MemoryMarshal.Cast<byte, uint>(
        data.AsSpan(hashOffset, hashSize)).ToArray();

    return (
        new BinaryTrie(nodes, edges),
        HashSet32.FromSortedHashes(hashes, header.MinWordLength, header.MaxWordLength)
    );
}
```

---

## 6. 성능 최적화 기법

### 6.1 Zero-Allocation with Span<T>

```csharp
// ❌ 기존 방식 - 매번 새 문자열 할당
public bool Contains(string text)
{
    for (int i = 0; i < text.Length; i++)
    {
        string substring = text.Substring(i);  // 힙 할당 발생!
        if (Match(substring)) return true;
    }
}

// ✅ Span 방식 - Zero Allocation
public bool Contains(ReadOnlySpan<char> text)
{
    for (int i = 0; i < text.Length; i++)
    {
        var slice = text.Slice(i);  // 스택에서 처리, 힙 할당 없음!
        if (Match(slice)) return true;
    }
}
```

### 6.2 Stack Allocation for Small Buffers

```csharp
private const int StackAllocThreshold = 512;

public bool Contains(ReadOnlySpan<char> text)
{
    if (_normalizer == null)
        return _matcher.Contains(text);

    // 작은 텍스트: 스택에 버퍼 할당 (힙 할당 없음)
    if (text.Length <= StackAllocThreshold)
    {
        Span<char> buffer = stackalloc char[text.Length];
        int len = _normalizer.Normalize(text, buffer);
        return _matcher.Contains(buffer.Slice(0, len));
    }
    else
    {
        // 큰 텍스트: ArrayPool에서 대여 (재사용)
        char[] rentedBuffer = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            int len = _normalizer.Normalize(text, rentedBuffer);
            return _matcher.Contains(rentedBuffer.AsSpan(0, len));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rentedBuffer);
        }
    }
}
```

### 6.3 ArrayPool<T> 활용

```csharp
// ArrayPool은 배열을 재사용하여 GC 압력을 줄입니다
char[] buffer = ArrayPool<char>.Shared.Rent(1024);  // 대여
try
{
    // buffer 사용...
}
finally
{
    ArrayPool<char>.Shared.Return(buffer);  // 반환 (재사용됨)
}
```

### 6.4 SSE4.2 하드웨어 가속 CRC32

```csharp
public static uint ComputeBytes(ReadOnlySpan<byte> data)
{
#if NET8_0_OR_GREATER
    // SSE4.2 지원 시 하드웨어 가속 사용
    if (Sse42.IsSupported)
    {
        return ComputeHardware(data);
    }
#endif
    // 소프트웨어 폴백
    return ComputeSoftware(data);
}

private static uint ComputeHardware(ReadOnlySpan<byte> data)
{
    uint crc = 0xFFFFFFFF;
    int offset = 0;

    // 8바이트씩 처리 (64비트 모드)
    if (Sse42.X64.IsSupported)
    {
        while (data.Length - offset >= 8)
        {
            ulong value = MemoryMarshal.Read<ulong>(data.Slice(offset, 8));
            crc = (uint)Sse42.X64.Crc32(crc, value);  // 단일 CPU 명령어!
            offset += 8;
        }
    }

    // 4바이트씩 처리
    while (data.Length - offset >= 4)
    {
        uint value = MemoryMarshal.Read<uint>(data.Slice(offset, 4));
        crc = Sse42.Crc32(crc, value);
        offset += 4;
    }

    // 나머지 바이트 처리
    while (offset < data.Length)
    {
        crc = Sse42.Crc32(crc, data[offset]);
        offset++;
    }

    return crc ^ 0xFFFFFFFF;
}
```

### 6.5 AggressiveInlining

```csharp
// 자주 호출되는 작은 메서드에 인라이닝 힌트
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool Contains(ReadOnlySpan<char> text)
{
    // 함수 호출 오버헤드 제거
}
```

### 6.6 Binary Search 최적화

```csharp
// 비트 시프트로 나누기 연산 최적화
int mid = (left + right) >> 1;  // (left + right) / 2 보다 빠름
```

---

## 7. API 설계

### 7.1 Public API

```csharp
public sealed class ProfanityFilter : IDisposable
{
    // 로딩
    public static ProfanityFilter Load(string binaryPath, LoadOptions? options = null);
    public static ProfanityFilter Load(Stream stream, LoadOptions? options = null);
    public static ProfanityFilter LoadFromBytes(byte[] data, LoadOptions? options = null);

    // 검사 (Zero-allocation)
    public bool Contains(ReadOnlySpan<char> text);
    public bool Contains(string text);

    // 마스킹
    public string Mask(string text, char maskChar = '*', MaskOptions? options = null);

    // 상세 매칭
    public int FindMatches(ReadOnlySpan<char> text, Span<MatchResult> results);

    // 리소스 해제
    public void Dispose();
}
```

### 7.2 옵션 클래스

```csharp
public sealed class LoadOptions
{
    public static readonly LoadOptions Default = new();

    // 대소문자 정규화 활성화 (기본: true)
    public bool EnableNormalization { get; init; } = true;
}

public sealed class MaskOptions
{
    public static readonly MaskOptions Default = new();

    // 고정 마스크 문자열 (null이면 길이 유지)
    public string? FixedMask { get; init; }
}
```

### 7.3 사용 예시

```csharp
// 1. 필터 로드
using var filter = ProfanityFilter.Load("badwords.bin");

// 2. 비속어 검사 (Zero-allocation)
bool hasProfanity = filter.Contains(message.AsSpan());

// 3. 마스킹
string censored = filter.Mask("this is badword", '*');
// 결과: "this is *******"

// 4. 고정 마스크
string censored2 = filter.Mask("this is badword", '*',
    new MaskOptions { FixedMask = "[삭제됨]" });
// 결과: "this is [삭제됨]"

// 5. 상세 매칭 정보
Span<MatchResult> results = stackalloc MatchResult[64];
int count = filter.FindMatches("bad and spam", results);
// results[0]: StartIndex=0, Length=3 ("bad")
// results[1]: StartIndex=8, Length=4 ("spam")
```

---

## 부록: 성능 벤치마크 목표

| 메트릭 | 목표 |
|--------|------|
| Contains 메모리 할당 | **0 bytes** |
| Contains 처리량 (50자) | **>1M ops/sec** |
| 로드 시간 (10K 단어) | **<50ms** |
| 바이너리 크기 (10K 단어) | **<500KB** |

---

## 참고 자료

- [Trie Data Structure](https://en.wikipedia.org/wiki/Trie)
- [CRC32 Algorithm](https://en.wikipedia.org/wiki/Cyclic_redundancy_check)
- [Span<T> and Memory<T>](https://docs.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
- [SSE4.2 CRC32 Intrinsics](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.x86.sse42)
