# ✅ ASSET SOURCE - Bundle Check Fix

## 🎯 Issue Identified & Fixed

**Problem:** Khi fetch API không check với Bundle
- Chỉ check Cache, không check AssetBundle
- Chiến lược loading không rõ ràng

**Solution:** Thêm Bundle check vào FetchSubjectsCoroutine

---

## 📝 Changes Made

### 1. **PDFSubjectService.cs** - FetchSubjectsCoroutine

**Before:** Chỉ check Cache
```csharp
// Check cache by cloudinaryFolder first, then by name
var cachedData = cacheManifest.GetSubjectCacheByFolder(pdfInfo.cloudinaryFolder) 
                 ?? cacheManifest.GetSubjectCache(pdfInfo.name);
if (cachedData != null)
{
    // ... cache check logic
}
```

**After:** Check Bundle trước, rồi Cache
```csharp
// PRIORITY 1: Check for AssetBundle first
if (checkLocalBundleFirst)
{
    string bundleName = SanitizeFolderName(pdfInfo.cloudinaryFolder).ToLower();
    string bundlePath = Path.Combine(Application.streamingAssetsPath, bundleStorePath, bundleName);
    
    if (File.Exists(bundlePath))
    {
        remoteSubject.isCached = true;  // Mark as ready (Bundle loaded on-demand)
        Log($"[BUNDLE CHECK] '{pdfInfo.name}': ✓ Bundle found - Will use BUNDLE");
    }
    else
    {
        Log($"[BUNDLE CHECK] '{pdfInfo.name}': ✗ Bundle not found");
        
        // PRIORITY 2: Fallback to check cache
        // ... cache check logic
    }
}
else
{
    // Bundle check disabled, go straight to cache
    // ... cache check logic
}
```

**Key Difference:**
```
BEFORE:
  Bundle? (ignored)
  → Cache? → YES/NO

AFTER:
  Bundle? → YES (use it) / NO → Cache? → YES/NO
```

---

### 2. **New Editor Tools** - Added to `_Tool/Editor/`

#### **A. AssetSourceViewer.cs** - Window Inspector
```
Menu: Window > DreamClass > Asset Source Viewer

Shows:
  ✓ Current strategy (Bundle → Cache → API)
  ✓ Bundle settings & path status
  ✓ Cache settings & path status
  ✓ Asset loading priority
  ✓ Remote subjects list
  ✓ Debug buttons (open folders, print stats)
```

#### **B. AssetSourceAnalyzer.cs** - Detailed Analysis
```
Menu: Window > DreamClass > Asset Source Analyzer
      Assets > DreamClass > Asset Source Analyzer

Prints to Console:
  ✓ Configuration check
  ✓ Bundle directory analysis
  ✓ Cache directory analysis
  ✓ Asset loading priority
  ✓ Recommendations & warnings
```

---

## 🔍 Log Format - Bundle Check

### Startup (API Fetch)

**When Bundle Found:**
```
[BUNDLE CHECK] 'SGK TOAN 11': ✓ Bundle found at sgk-toan-11 - Will use BUNDLE
```

**When Bundle Not Found (Fallback to Cache):**
```
[BUNDLE CHECK] 'SGK TOAN 11': ✗ Bundle not found at sgk-toan-11
[CACHE CHECK] 'SGK TOAN 11': fullyCached=true, hashMatch=true, filesExist=true, isCached=true
[CACHE CHECK] 'SGK TOAN 11': ✓ Cache found - Assigned 102 cached image paths
```

**When Bundle & Cache Not Found:**
```
[BUNDLE CHECK] 'SGK TOAN 11': ✗ Bundle not found at sgk-toan-11
[CACHE CHECK] 'SGK TOAN 11': ✗ No cache data found - Will download from API
```

---

## 🎯 Asset Loading Priority (After Fix)

### **Priority 1: AssetBundle** ⚡⚡⚡
```
checkLocalBundleFirst = true?
  YES → Check if bundle file exists in StreamingAssets/
    YES → LOAD FROM BUNDLE (fastest, ~0.5-1 sec)
    NO  → FALLBACK TO PRIORITY 2
  NO  → SKIP TO PRIORITY 2
```

### **Priority 2: Local Cache** ⚡⚡
```
Check if cache manifest has cached images?
  YES → LOAD FROM CACHE (medium, ~1-3 sec)
  NO  → FALLBACK TO PRIORITY 3
```

### **Priority 3: API Fetch** ⚡
```
DOWNLOAD FROM API (slow, ~5-10+ sec)
  → Auto-cache images if autoCacheAfterFetch = true
```

---

## 🔧 Editor Tools Usage

### **AssetSourceViewer Window**
```
1. Menu > Window > DreamClass > Asset Source Viewer
2. See all settings displayed
3. Check Bundle/Cache status
4. Click buttons to open folders
```

### **AssetSourceAnalyzer**
```
1. Menu > Window > DreamClass > Asset Source Analyzer
2. Get detailed console report
3. See bundle files list
4. Get recommendations
```

---

## 📊 Example Output

### Scenario 1: Bundle Available
```
[BUNDLE CHECK] 'Math Grade 11': ✓ Bundle found at math-grade-11 - Will use BUNDLE
  → No cache check (Bundle has priority)
```

### Scenario 2: Bundle Not Found, Cache Available
```
[BUNDLE CHECK] 'Math Grade 11': ✗ Bundle not found at math-grade-11
[CACHE CHECK] 'Math Grade 11': fullyCached=true, hashMatch=true, filesExist=true, isCached=true
[CACHE CHECK] 'Math Grade 11': ✓ Cache found - Assigned 100 cached image paths
  → Will use CACHE (fallback)
```

### Scenario 3: Neither Bundle nor Cache
```
[BUNDLE CHECK] 'Math Grade 11': ✗ Bundle not found at math-grade-11
[CACHE CHECK] 'Math Grade 11': ✗ No cache data found - Will download from API
  → Will FETCH FROM API (fallback)
```

---

## ✅ Verification

- [x] Bundle check added to FetchSubjectsCoroutine
- [x] Logs show Bundle → Cache priority
- [x] Editor tool AssetSourceViewer created
- [x] Editor tool AssetSourceAnalyzer created
- [x] Fixed unused variable warning
- [x] No compilation errors

---

## 🚀 Benefits

1. **Clear Priority:** Bundle → Cache → API (in order)
2. **Transparent Logging:** See exactly which asset source is used
3. **Easy Debugging:** Two editor tools for monitoring
4. **Performance:** Bundle is checked, fastest option used first
5. **Fallback:** Graceful degradation if preferred source not available

---

## 📁 Files Modified/Created

```
Assets/
├── _Data/_LearningLecture/Network/
│   └── PDFSubjectService.cs          ✅ Modified - Added Bundle check to Fetch
│
└── _Tool/Editor/
    ├── AssetSourceViewer.cs          ✨ NEW - Editor window for monitoring
    └── AssetSourceAnalyzer.cs        ✨ NEW - Console analysis tool
```

---

## 💡 How It Works Now

### At Startup (API Fetch)
```
1. Try to fetch from API
2. For each subject in API response:
   a. Check: Does bundle file exist?
      → YES: Mark as ready (isCached=true)
      → NO: Continue to step b
   b. Check: Does cache exist?
      → YES: Mark as ready (isCached=true)
      → NO: Will need to download
3. Then auto-cache any missing items
```

### When User Clicks Subject
```
1. LoadSubjectSpritesOnDemand(subject)
2. Check: Does bundle file exist?
   → YES: Load from BUNDLE ⚡⚡⚡
   → NO: Continue to step 3
3. Check: Is subject marked as cached?
   → YES: Load from CACHE ⚡⚡
   → NO: Download from API ⚡
```

---

## 📚 Logs to Monitor

| Log | Meaning |
|-----|---------|
| `[BUNDLE CHECK] ✓ Bundle found` | Bundle exists, will be used |
| `[BUNDLE CHECK] ✗ Bundle not found` | Bundle missing, fallback to cache |
| `[CACHE CHECK] ✓ Cache found` | Cache exists, will be used |
| `[CACHE CHECK] ✗ No cache data` | No cache, will download from API |

---

## 🎓 Summary

**Before Fix:**
- API fetch only checked Cache
- Bundle check only during lazy load
- Unclear priority

**After Fix:**
- API fetch checks Bundle FIRST
- Then Cache as fallback
- Then API as last resort
- Clear logged priority (Bundle → Cache → API)
- Two editor tools for monitoring
