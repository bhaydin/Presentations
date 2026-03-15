# Demo API Quick Start: OpenAI vs Azure OpenAI

## TL;DR - Which Should I Use?

### Use OpenAI if:
✅ You want the **fastest setup** (1 environment variable)  
✅ You have an OpenAI API key already  
✅ You're doing this for a **personal demo**  
✅ You want to **minimize configuration**  

### Use Azure OpenAI if:
✅ Your company already has **Azure resources**  
✅ You want to show **enterprise patterns**  
✅ You need to demonstrate **Azure integration**  
✅ You're already familiar with Azure setup  

**Bottom line:** Both work identically for the demos. OpenAI is simpler.

---

## OpenAI Setup (Recommended for Demos)

### Step 1: Get Your API Key
1. Go to https://platform.openai.com/api-keys
2. Create a new API key
3. Copy it (starts with `sk-`)

### Step 2: Set Environment Variable
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

### Step 3: Install Package
```bash
dotnet add package Microsoft.Extensions.AI.OpenAI
```

### Step 4: Update Program.cs
```csharp
using Microsoft.Extensions.AI;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new Exception("Set OPENAI_API_KEY");

var chatClient = new OpenAIClient(apiKey).AsChatClient("gpt-4o");
```

**That's it!** ✅

---

## Azure OpenAI Setup

### Step 1: Get Your Azure Resources
You need three pieces of information from Azure Portal:
1. **Endpoint:** `https://your-resource.openai.azure.com/`
2. **API Key:** From "Keys and Endpoint" section
3. **Deployment Name:** The name you gave your model deployment

### Step 2: Set Environment Variables
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_KEY="your-key-here"
export AZURE_OPENAI_DEPLOYMENT="gpt-4"
```

### Step 3: Install Packages
```bash
dotnet add package Microsoft.Extensions.AI.OpenAI
dotnet add package Azure.AI.OpenAI
```

### Step 4: Update Program.cs
```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new Exception("Set AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") 
    ?? throw new Exception("Set AZURE_OPENAI_KEY");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4";

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
var chatClient = azureClient.AsChatClient(deploymentName);
```

---

## Side-by-Side Comparison

| Aspect | OpenAI | Azure OpenAI |
|--------|--------|--------------|
| **Setup Time** | 2 minutes | 5 minutes |
| **Environment Variables** | 1 | 3 |
| **NuGet Packages** | 1 | 2 |
| **Code Complexity** | 3 lines | 6 lines |
| **Cost** | Pay-as-you-go | Pay-as-you-go |
| **Model Options** | `gpt-4o`, `gpt-4`, `gpt-3.5-turbo` | Your deployed models |
| **Enterprise Features** | Standard | Enhanced (VNet, managed identity, etc.) |
| **Demo Suitability** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |

---

## Common Issues & Solutions

### OpenAI

**Issue:** `401 Unauthorized`  
**Fix:** Check your API key is correct and starts with `sk-`

**Issue:** `Rate limit exceeded`  
**Fix:** You might be on the free tier. Wait or upgrade.

**Issue:** `Model not found`  
**Fix:** Use `gpt-4o` or `gpt-3.5-turbo` (not custom deployment names)

### Azure OpenAI

**Issue:** `404 Not Found`  
**Fix:** Check your endpoint URL is correct (should end with `.openai.azure.com/`)

**Issue:** `DeploymentNotFound`  
**Fix:** Use the exact deployment name from Azure Portal, not the model name

**Issue:** `Invalid credentials`  
**Fix:** Make sure you're using the API key, not the Azure subscription key

---

## Switching Between Them

The beauty of `Microsoft.Extensions.AI` is that **switching is just changing the client creation**. All your other code stays identical.

### Demo 1 Switch Example

**OpenAI version:**
```csharp
var chatClient = new OpenAIClient(apiKey).AsChatClient("gpt-4o");
var agent = new SupportAgent(chatClient);
```

**Azure version:**
```csharp
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
var chatClient = azureClient.AsChatClient(deploymentName);
var agent = new SupportAgent(chatClient);
```

**Everything else is identical!** The eval logic, scoring, golden prompts - all the same.

---

## Recommendation for Your Talk

**For Chippewa Valley Code Camp on March 14:**

Use **OpenAI** because:
1. ✅ Simpler setup = less that can go wrong
2. ✅ Fewer environment variables to set
3. ✅ Faster to explain if someone asks
4. ✅ Works on any WiFi (no Azure VPN issues)
5. ✅ You can still say "This works with Azure OpenAI too - just swap the client"

**In your talk, mention:** 
"I'm using OpenAI for this demo, but everything you see here works identically with Azure OpenAI. The abstractions in Microsoft.Extensions.AI mean you just swap the client creation - all your eval code, scoring logic, and test harnesses stay exactly the same."

---

## Pre-Demo Checklist

### Night Before
- [ ] Test your API key works: `curl https://api.openai.com/v1/models -H "Authorization: Bearer $OPENAI_API_KEY"`
- [ ] Run both demos end-to-end at least once
- [ ] Check your OpenAI account has sufficient credits
- [ ] Save your API key in a secure note (in case you need to re-export it)

### Morning Of
- [ ] Export environment variable in your terminal
- [ ] Test with a simple curl or quick .NET console app
- [ ] Keep your API key handy (but don't share screen when showing it!)

---

## Cost Estimate

Both demos are **very cheap** to run:

**Demo 1 (Regression Harness):**
- 5-10 test prompts
- ~2-3 model calls per test
- Total: ~15-30 calls
- Cost: **< $0.10**

**Demo 2 (Trace Inspection):**
- 1-2 agent runs
- Each with 2-3 model calls
- Total: ~4-6 calls
- Cost: **< $0.05**

**Total demo cost: ~$0.15** 

You could run these demos 100 times for $15. Don't stress about cost.

---

## Final Recommendation

**Go with OpenAI.** It's simpler, faster, and just as effective for teaching the concepts. You can always mention Azure as an option without complicating your setup.

