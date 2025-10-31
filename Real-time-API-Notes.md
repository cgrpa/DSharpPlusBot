1. You can also reference /OpenAI-CSharpRealtimeWPFDEmo if you run into issues implementing the Realtime API side of things.

2. Information From Perplexity:
## Integrating gpt-realtime-mini into .NET Applications

The **gpt-realtime-mini** (also known as `gpt-4o-mini-realtime-preview`) can be integrated into .NET applications using WebSocket or WebRTC protocols for low-latency, speech-in/speech-out conversational interactions.[1][2]

### Implementation Approaches

**Using Azure.AI.OpenAI NuGet Package (.NET 9)**

The official OpenAI .NET library (version 2.1.0-beta.1 and later) provides preview support for the Realtime API. Here's the basic implementation pattern:[3]

1. **Install required packages:**
   - `Azure.AI.OpenAI` (v2.1.0-beta.1 or later)
   - `NAudio` (for audio capture/playback)
   - Optional: `Spectre.Console` for enhanced console UI[4]

2. **Connect via WebSocket:**
   ```csharp
   var endpoint = "wss://<your-resource>.openai.azure.com/openai/realtime?api-version=2024-10-01-preview&deployment=gpt-4o-mini-realtime-preview";
   ```

3. **Session Configuration:**
   Send a `session.update` command to configure voice, instructions, audio format (24KHz 16-bit mono PCM), and turn detection mode.[3]

4. **Audio Input:**
   Use `input_audio_buffer.append` commands to send audio data in real-time.[3]

5. **Handle Responses:**
   Process WebSocket events including `response.audio.delta` for streaming audio output, `response.text.delta` for text, and function calling events.[3]

### Semantic Kernel Support Status

**Currently: Python Only** - As of October 2024, Semantic Kernel only supports Realtime API in **Python** (version 1.23.0+), not in .NET/C#. The Semantic Kernel blog announcement and documentation explicitly state this is a Python-only feature with clients like `AzureRealtimeWebsocket`, `OpenAIRealtimeWebsocket`, and `OpenAIRealtimeWebRTC`.[5][6]

The .NET version of Semantic Kernel does **not** have native Realtime API support as of the latest information available. The roadmap indicates the Agent Framework is reaching GA in Q1 2025, but Realtime API integration for C# wasn't mentioned.[7]

### Alternative: Manual Integration with AIFunctions

If you need to use Semantic Kernel patterns in .NET, developer Mehran Davoudi created a workaround using `Microsoft.Extensions.AI`. This approach:[8]

**Converts AIFunctions to Realtime Tools:**
```csharp
public static ConversationFunctionTool ConversationTool(this AIFunction function) =>
    new(function.Name)
    {
        Description = function.Description,
        Parameters = BinaryData.FromString(function.JsonSchema.ToString())
    };
```

**Benefits:**
- Reuses existing `AIFunction` definitions[8]
- Maintains strong typing and descriptions[8]
- Avoids manual JSON schema duplication[8]
- Automatically includes enums and metadata[8]

**Implementation Pattern:**
```csharp
var tools = GetTools(); // AIFunction[]
var conversationTools = tools.Select(t => t.ConversationTool());
sessionOptions.Tools.AddRange(conversationTools);

// When tool is invoked
var tool = tools.First(t => t.Name == functionName);
var result = await tool.InvokeAsync(parsedArgs);
await session.AddItemAsync(RealtimeItem.CreateFunctionCallOutput(callId, result?.ToString() ?? ""));
```

### Key Implementation Details

**Supported Models:**
- `gpt-4o-realtime-preview` (2024-12-17)
- `gpt-4o-mini-realtime-preview` (2024-12-17)
- `gpt-realtime` (2025-08-28)
- `gpt-realtime-mini` (2025-10-06)[2][1]

**API Version:**
Use `2025-04-01-preview` for Azure OpenAI.[2]

**Regions:**
Deploy to East US 2 or Sweden Central for Azure OpenAI.[2]

**Turn Detection Modes:**
- `server_vad`: Automatic voice activity detection triggers responses[3]
- `none`: Manual control via `response.create` commands for push-to-talk scenarios[3]

**Function Calling:**
After receiving `response.done` confirming all tool calls, add function outputs using `conversation.item.create` with type `function_call_output`, then trigger `response.create`.[3]

### Production Considerations

The Realtime API is designed for **server-to-server** scenarios, not direct client connections. Your architecture should include:[3]
- Middle-tier service managing WebSocket connections
- User authentication/validation layer
- Audio handling between browser/app and your service[9]

### Reference Implementation

Check the Santa Claus demo on GitHub (tsjdev-apps/santaclaus-realtime-api) for a complete .NET 9 console application showing audio input/output, function calling, and session management.[4]

**Bottom line:** While Semantic Kernel doesn't support Realtime API in .NET yet, you can integrate it directly using the official OpenAI .NET library or adapt the AIFunctions pattern to bridge the gap until official SK support arrives.

[1](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/realtime-audio-quickstart)
[2](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/realtime-audio-websockets)
[3](https://github.com/Azure-Samples/aoai-realtime-audio-sdk)
[4](https://github.com/tsjdev-apps/santaclaus-realtime-api)
[5](https://devblogs.microsoft.com/semantic-kernel/talk-to-your-agents-introducing-the-realtime-apis-in-semantic-kernel/)
[6](https://devblogs.microsoft.com/semantic-kernel/tag/realtime-api/)
[7](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-roadmap-h1-2025-accelerating-agents-processes-and-integration/)
[8](https://dev.to/mehrandvd/using-aifunctions-with-openai-realtime-api-in-net-261k)
[9](https://thecodepoet.net/blog/realtime-api/)
[10](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/realtime-audio)
[11](https://github.com/openai/openai-dotnet)
[12](https://platform.openai.com/docs/guides/realtime)
[13](https://www.genspark.ai/spark/c-%E3%81%A7%E3%81%AEopenai-realtime-api%E5%AE%9F%E8%A3%85%E3%82%AC%E3%82%A4%E3%83%89/b7d09537-eb9e-4e41-a994-5df1373fd8f5)
[14](https://www.linkedin.com/posts/ollip_talk-to-your-agents-introducing-the-realtime-activity-7303685145423708160-9jtg)
[15](https://github.com/microsoft/semantic-kernel/issues/9075)
[16](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/realtime)
[17](https://www.youtube.com/watch?v=foSnCW9qxQA)
[18](https://www.youtube.com/watch?v=ms1xEFrgkbM)
[19](https://github.com/microsoft/semantic-kernel/blob/main/python/samples/concepts/realtime/realtime_agent_with_function_calling_websocket.py)
[20](https://www.linkedin.com/posts/mehrandvd_using-aifunctions-with-openai-realtime-api-activity-7371120211304820737-jcCU)
[21](https://github.com/microsoft/semantic-kernel/issues/10073)
[22](https://openai.com/index/introducing-gpt-realtime/)
[23](https://platform.openai.com/docs/api-reference)
[24](https://www.linkedin.com/posts/sergiogonzalezjimenez_talk-to-your-agents-introducing-the-realtime-activity-7303717444345368579-LZVj)
[25](https://www.youtube.com/watch?v=OlJSTTof9mo)
[26](https://devblogs.microsoft.com/semantic-kernel/using-openais-audio-preview-model-with-semantic-kernel/)
[27](https://www.datacamp.com/tutorial/realtime-api-openai)
[28](https://stackoverflow.com/questions/79129711/how-to-call-chatgpt-assistants-api-v2-from-semantic-kernel-in-c-sharp)
[29](https://www.youtube.com/watch?v=YLv5z6NI494)
[30](https://github.com/microsoft/semantic-kernel)
[31](https://community.openai.com/t/realtime-websocket-confusion/1095089)
[32](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
[33](https://moimhossain.com/2025/05/08/implementing-rag-with-webrtc-and-azure-ai-search/)
[34](https://developer.mamezou-tech.com/en/blogs/2024/12/21/openai-realtime-api-webrtc/)
[35](https://www.youtube.com/watch?v=Nfl9CblNBto)
[36](https://www.youtube.com/watch?v=H3ovjkBFOw4)
[37](https://oksala.net/2024/10/30/access-real-time-data-from-llm-using-semantic-kernel/)
[38](https://devblogs.microsoft.com/semantic-kernel/page/7/)
[39](https://devblogs.microsoft.com/semantic-kernel/category/announcements/)
[40](https://devblogs.microsoft.com/semantic-kernel/category/announcement/)