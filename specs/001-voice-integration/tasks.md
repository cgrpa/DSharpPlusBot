# Tasks: Discord Voice Integration

**Input**: Design documents from `/specs/001-voice-integration/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included based on constitution requirements (Testing Philosophy - NON-NEGOTIABLE). All services require integration tests, audio processing requires unit tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `TheSexy6BotWorker/`, `TheSexy6BotWorker.Tests/` at repository root
- Paths shown below use absolute paths from repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Add DSharpPlus.VoiceNext NuGet package version 4.5.1 to TheSexy6BotWorker/TheSexy6BotWorker.csproj
- [X] T002 [P] Add NAudio NuGet package version 2.2.1 to TheSexy6BotWorker/TheSexy6BotWorker.csproj
- [X] T003 [P] Update Dockerfile to install native dependencies (libopus0, libsodium23, ffmpeg) in TheSexy6BotWorker/Dockerfile
- [X] T004 [P] Create Voice directory for DTOs in TheSexy6BotWorker/DTOs/Voice/
- [X] T005 [P] Create Voice directory for Services in TheSexy6BotWorker/Services/Voice/
- [X] T006 [P] Create Voice directory for test organization in TheSexy6BotWorker.Tests/Services/Voice/
- [X] T007 Configure user secrets for OpenAI Realtime API key and voice integration settings (manual step, document in CLAUDE.md update)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 Create SessionState enum in TheSexy6BotWorker/DTOs/Voice/SessionState.cs
- [X] T009 [P] Create MessageRole enum in TheSexy6BotWorker/DTOs/Voice/MessageRole.cs
- [X] T010 [P] Create FunctionCallStatus enum in TheSexy6BotWorker/DTOs/Voice/FunctionCallStatus.cs
- [X] T011 Create AudioFrame DTO in TheSexy6BotWorker/DTOs/Voice/AudioFrame.cs
- [X] T012 [P] Create VoiceSessionConfig DTO in TheSexy6BotWorker/DTOs/Voice/VoiceSessionConfig.cs
- [X] T013 [P] Create ConversationMessage DTO in TheSexy6BotWorker/DTOs/Voice/ConversationMessage.cs
- [X] T014 [P] Create FunctionCallData DTO in TheSexy6BotWorker/DTOs/Voice/FunctionCallData.cs
- [X] T015 Create VoiceSessionState DTO in TheSexy6BotWorker/DTOs/Voice/VoiceSessionState.cs
- [X] T016 [P] Create OpenAIRealtimeMessage DTO in TheSexy6BotWorker/DTOs/Voice/OpenAIRealtimeMessage.cs
- [X] T017 Implement AudioConverter service in TheSexy6BotWorker/Services/Voice/AudioConverter.cs (ToOpenAIFormat, ToDiscordFormat methods)
- [X] T018 Create unit tests for AudioConverter in TheSexy6BotWorker.Tests/Services/Voice/AudioConverterTests.cs
- [X] T019 Register AudioConverter as Transient service in TheSexy6BotWorker/Program.cs
- [X] T020 Register VoiceNext extension with EnableIncoming=true in TheSexy6BotWorker/DiscordWorker.cs ExecuteAsync method

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Summon Bot to Voice Channel (Priority: P1) 🎯 MVP

**Goal**: Bot can join and leave Discord voice channels via commands

**Independent Test**: Issue /voice-join command in Discord server and verify bot joins the voice channel. Issue /voice-leave and verify bot disconnects cleanly.

### Implementation for User Story 1

- [X] T021 [P] [US1] Create VoiceCommands command class in TheSexy6BotWorker/Commands/VoiceCommands.cs (skeleton with Command attributes)
- [X] T022 [P] [US1] Create VoiceSessionService interface IVoiceSessionService in TheSexy6BotWorker/Services/Voice/IVoiceSessionService.cs
- [X] T023 [US1] Implement VoiceSessionService constructor with DI in TheSexy6BotWorker/Services/Voice/VoiceSessionService.cs
- [X] T024 [US1] Implement CreateSessionAsync method in VoiceSessionService to initialize VoiceSessionState
- [X] T025 [US1] Implement GetSessionAsync method in VoiceSessionService to retrieve active sessions
- [X] T026 [US1] Add Discord voice connection logic to CreateSessionAsync using DSharpPlus VoiceNext
- [X] T027 [US1] Implement EndSessionAsync method in VoiceSessionService to cleanup resources
- [X] T028 [US1] Implement voice-join command in VoiceCommands.cs with precondition validation (user in channel, bot permissions)
- [X] T029 [US1] Implement voice-leave command in VoiceCommands.cs with session cleanup
- [X] T030 [US1] Add LOCAL_DEV mode support for test-voice-join and test-voice-leave command aliases
- [X] T031 [US1] Register VoiceSessionService as Singleton in TheSexy6BotWorker/Program.cs
- [X] T032 [US1] Register VoiceCommands in command processor in TheSexy6BotWorker/DiscordWorker.cs
- [X] T033 [US1] Add structured logging for voice session lifecycle events in VoiceSessionService
- [X] T034 [US1] Create integration tests for voice connection lifecycle in TheSexy6BotWorker.Tests/Services/Voice/Integration/VoiceConnectionTests.cs
- [X] T035 [US1] Add error handling for missing permissions and invalid state in VoiceCommands
- [X] T036 [US1] Update CLAUDE.md with voice command usage and session management patterns

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently (bot can join/leave voice channels)

---

## Phase 4: User Story 2 - Voice-to-AI Conversation (Priority: P2)

**Goal**: Users can speak and receive AI responses via voice

**Independent Test**: Speak into microphone while bot is in voice channel and verify AI responds with audio output.

### Implementation for User Story 2

- [ ] T037 [P] [US2] Create ConversationContextManager service in TheSexy6BotWorker/Services/Voice/ConversationContextManager.cs
- [ ] T038 [P] [US2] Create OpenAIRealtimeClient interface IOpenAIRealtimeClient in TheSexy6BotWorker/Services/Voice/IOpenAIRealtimeClient.cs
- [ ] T039 [US2] Implement OpenAIRealtimeClient constructor with WebSocket initialization in TheSexy6BotWorker/Services/Voice/OpenAIRealtimeClient.cs
- [ ] T040 [US2] Implement ConnectAsync method in OpenAIRealtimeClient (WebSocket connection to OpenAI)
- [ ] T041 [US2] Implement session.update event sender in OpenAIRealtimeClient (configure session with tools)
- [ ] T042 [US2] Implement HandleServerMessageAsync method in OpenAIRealtimeClient (event router)
- [ ] T043 [US2] Implement audio delta handler (response.audio.delta event) in OpenAIRealtimeClient
- [ ] T044 [US2] Implement speech detection handlers (input_audio_buffer.speech_started/stopped) in OpenAIRealtimeClient
- [ ] T045 [US2] Implement SendAudioAsync method in OpenAIRealtimeClient (input_audio_buffer.append)
- [ ] T046 [US2] Add Discord VoiceReceived event handler to capture user speech in VoiceSessionService
- [ ] T047 [US2] Integrate AudioConverter in audio receive pipeline (Discord 48kHz → OpenAI 24kHz) in VoiceSessionService
- [ ] T048 [US2] Add Discord audio playback via VoiceTransmitSink in VoiceSessionService
- [ ] T049 [US2] Integrate AudioConverter in audio transmit pipeline (OpenAI 24kHz → Discord 48kHz) in VoiceSessionService
- [ ] T050 [US2] Connect OpenAIRealtimeClient to VoiceSessionService CreateSessionAsync method
- [ ] T051 [US2] Implement DisconnectAsync method in OpenAIRealtimeClient with cleanup
- [ ] T052 [US2] Add conversation context tracking in ConversationContextManager (AddMessage, GetHistory methods)
- [ ] T053 [US2] Register OpenAIRealtimeClient as Singleton in TheSexy6BotWorker/Program.cs
- [ ] T054 [US2] Register ConversationContextManager as Singleton in TheSexy6BotWorker/Program.cs
- [ ] T055 [US2] Add structured logging for WebSocket events and audio processing in OpenAIRealtimeClient
- [ ] T056 [US2] Create integration tests for OpenAI WebSocket communication in TheSexy6BotWorker.Tests/Services/Voice/Integration/OpenAIRealtimeClientTests.cs
- [ ] T057 [US2] Add error handling for WebSocket disconnections with exponential backoff in OpenAIRealtimeClient
- [ ] T058 [US2] Implement audio buffer management and queuing in VoiceSessionService

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently (bot joins channel + users can speak and hear AI responses)

---

## Phase 5: User Story 3 - Natural Conversation Flow (Priority: P3)

**Goal**: Smooth, low-latency interactions with context awareness

**Independent Test**: Conduct multi-turn conversation and verify response latency < 3 seconds, context maintained across exchanges, and interruption handling works.

### Implementation for User Story 3

- [ ] T059 [P] [US3] Implement Server VAD configuration in session.update event (turn_detection settings) in OpenAIRealtimeClient
- [ ] T060 [P] [US3] Add conversation context restoration logic in ConversationContextManager
- [ ] T061 [US3] Implement user interruption detection (input_audio_buffer.speech_started during playback) in VoiceSessionService
- [ ] T062 [US3] Add audio playback cancellation on user interruption in VoiceSessionService
- [ ] T063 [US3] Implement transcription logging (conversation.item.input_audio_transcription.completed) in OpenAIRealtimeClient
- [ ] T064 [US3] Add response latency tracking and logging in VoiceSessionService
- [ ] T065 [US3] Implement context summarization when approaching token limits in ConversationContextManager
- [ ] T066 [US3] Add audio quality validation and monitoring in AudioConverter
- [ ] T067 [US3] Implement response timeout handling (max 10 seconds wait) in VoiceSessionService
- [ ] T068 [US3] Add conversation history persistence to VoiceSessionState in ConversationContextManager
- [ ] T069 [US3] Create performance tests for latency and context retention in TheSexy6BotWorker.Tests/Services/Voice/Integration/PerformanceTests.cs

**Checkpoint**: All conversation quality improvements should now be functional (low latency, context awareness, interruption handling)

---

## Phase 6: User Story 4 - Bot Dismissal and Cleanup (Priority: P4)

**Goal**: Automatic and manual cleanup with resource management

**Independent Test**: Issue dismiss command and verify clean disconnect. Verify auto-disconnect after timeout. Verify all resources released.

### Implementation for User Story 4

- [ ] T070 [P] [US4] Implement voice-status command in VoiceCommands.cs (display active session info)
- [ ] T071 [P] [US4] Implement session timeout tracking in VoiceSessionState
- [ ] T072 [US4] Add auto-disconnect timer based on silence duration in VoiceSessionService
- [ ] T073 [US4] Implement auto-disconnect when all users leave channel (participant count = 0) in VoiceSessionService
- [ ] T074 [US4] Add session metrics calculation (duration, message count, cost estimate) in VoiceSessionService
- [ ] T075 [US4] Implement resource cleanup verification in EndSessionAsync (WebSockets, audio streams, buffers)
- [ ] T076 [US4] Add session summary response formatting in voice-leave command
- [ ] T077 [US4] Create unit tests for session timeout logic in TheSexy6BotWorker.Tests/Services/Voice/VoiceSessionServiceTests.cs
- [ ] T078 [US4] Add graceful shutdown handling for voice sessions in DiscordWorker StopAsync

**Checkpoint**: All cleanup and resource management features should be functional

---

## Phase 7: Function Calling Integration (Cross-Cutting)

**Purpose**: Enable AI to invoke Semantic Kernel plugins via voice

- [ ] T079 [P] Create tool definition mapper for WeatherService in VoiceSessionService
- [ ] T080 [P] Create tool definition mapper for PerplexitySearchService in VoiceSessionService
- [ ] T081 Implement function call handler (response.function_call_arguments.done) in OpenAIRealtimeClient
- [ ] T082 Add Semantic Kernel plugin invocation logic in VoiceSessionService ExecuteFunctionAsync method
- [ ] T083 Implement function result sender (conversation.item.create with function_call_output) in OpenAIRealtimeClient
- [ ] T084 Add function call logging and error handling in VoiceSessionService
- [ ] T085 Create integration tests for voice-triggered function calls in TheSexy6BotWorker.Tests/Services/Voice/Integration/VoiceFunctionCallingTests.cs

---

## Phase 8: Cost Management and Configuration (Cross-Cutting)

**Purpose**: Cost tracking and per-server configuration

- [ ] T086 [P] Implement voice-config command in VoiceCommands.cs (view/update settings, Admin only)
- [ ] T087 [P] Create SessionCostTracker utility class in TheSexy6BotWorker/Services/Voice/SessionCostTracker.cs
- [ ] T088 Add cost calculation logic in SessionCostTracker based on token estimates
- [ ] T089 Implement per-server budget enforcement in VoiceSessionService
- [ ] T090 Add cost limit validation in CreateSessionAsync before allowing new sessions
- [ ] T091 Implement voice-stats command in VoiceCommands.cs (usage statistics, Admin only)
- [ ] T092 Add cost tracking to VoiceSessionState and log on session end
- [ ] T093 Create unit tests for cost calculation in TheSexy6BotWorker.Tests/Services/Voice/SessionCostTrackerTests.cs

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T094 [P] Add comprehensive XML documentation comments to all Voice services
- [ ] T095 [P] Add comprehensive XML documentation comments to all Voice DTOs
- [ ] T096 [P] Create README.md in specs/001-voice-integration/ summarizing implementation
- [ ] T097 Code cleanup and refactoring for voice integration components
- [ ] T098 Performance optimization for audio conversion pipeline
- [ ] T099 Add error scenario integration tests (network failure, API timeout, permission errors) in TheSexy6BotWorker.Tests/Services/Voice/Integration/ErrorScenarioTests.cs
- [ ] T100 Security hardening (validate all user inputs, rate limiting for voice commands)
- [ ] T101 Update CLAUDE.md with complete voice integration patterns and troubleshooting guide
- [ ] T102 Manual testing in real Discord voice channels with multiple users
- [ ] T103 Manual testing of cost limits and budget enforcement
- [ ] T104 Manual testing of network instability scenarios (disconnect/reconnect)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - US1 (P1) → Can start after Foundational - No dependencies on other stories
  - US2 (P2) → Depends on US1 (requires bot in voice channel first)
  - US3 (P3) → Depends on US2 (requires conversation capability)
  - US4 (P4) → Depends on US1 (requires session management)
- **Function Calling (Phase 7)**: Depends on US2 completion
- **Cost Management (Phase 8)**: Depends on US1 completion
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories ✅ MVP
- **User Story 2 (P2)**: Depends on US1 (bot must join channel first)
- **User Story 3 (P3)**: Depends on US2 (requires voice conversation capability)
- **User Story 4 (P4)**: Depends on US1 (requires session management), can be parallel with US2/US3

### Within Each User Story

- DTOs completed first (foundational phase)
- Services before command handlers
- Core implementation before integration
- Tests can run in parallel if marked [P]
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002-T006)
- All DTO creation tasks in Foundational phase marked [P] can run in parallel (T009-T014, T016)
- Within US1: T021 and T022 can run in parallel
- Within US2: T037 and T038 can run in parallel
- Within US3: T059 and T060 can run in parallel
- Within US4: T070 and T071 can run in parallel
- Phase 7: T079 and T080 can run in parallel
- Phase 8: T086 and T087 can run in parallel
- Phase 9: T094 and T095 can run in parallel

---

## Parallel Example: User Story 1

```bash
# Tasks that can start simultaneously:
T021: Create VoiceCommands skeleton
T022: Create IVoiceSessionService interface

# Then sequentially:
T023: Implement VoiceSessionService constructor
T024-T027: Implement session management methods

# Then in parallel:
T028: Implement voice-join command
T029: Implement voice-leave command

# Final tasks:
T030-T036: Configuration, registration, tests, documentation
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T007)
2. Complete Phase 2: Foundational (T008-T020) - CRITICAL blocking phase
3. Complete Phase 3: User Story 1 (T021-T036)
4. **STOP and VALIDATE**: Test voice join/leave independently
5. Deploy/demo if ready (bot can join and leave voice channels via commands)

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 (T021-T036) → Test independently → Deploy/Demo (MVP - Bot joins voice!)
3. Add User Story 2 (T037-T058) → Test independently → Deploy/Demo (Bot has voice conversations!)
4. Add User Story 3 (T059-T069) → Test independently → Deploy/Demo (Smooth conversation flow!)
5. Add User Story 4 (T070-T078) → Test independently → Deploy/Demo (Automatic cleanup!)
6. Add Function Calling (T079-T085) → Test independently → Deploy/Demo (Voice-triggered tools!)
7. Add Cost Management (T086-T093) → Test independently → Deploy/Demo (Budget controls!)
8. Each phase adds value without breaking previous features

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T020)
2. Once Foundational is done:
   - Developer A: User Story 1 (T021-T036) - MVP
   - Developer B: Can start on Phase 7 (Function Calling) DTOs and planning
   - Developer C: Can work on Phase 8 (Cost Management) utilities
3. After US1 complete:
   - Developer A moves to US2 (T037-T058)
   - Developer B continues with Phase 7
   - Developer C works on US4 (T070-T078) since it only depends on US1
4. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- **Testing Philosophy (NON-NEGOTIABLE)**: Integration tests required for all voice services per constitution
- **Manual Testing Required**: Discord voice interactions must be manually validated per constitution
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- **Native Dependencies**: Ensure libopus, libsodium, ffmpeg installed before starting implementation

---

## Task Summary

**Total Tasks**: 104

### Tasks Per User Story:
- Setup (Phase 1): 7 tasks
- Foundational (Phase 2): 13 tasks (BLOCKING - must complete before user stories)
- **User Story 1** (P1 - MVP): 16 tasks (T021-T036)
- **User Story 2** (P2): 22 tasks (T037-T058)
- **User Story 3** (P3): 11 tasks (T059-T069)
- **User Story 4** (P4): 9 tasks (T070-T078)
- Function Calling: 7 tasks (T079-T085)
- Cost Management: 8 tasks (T086-T093)
- Polish: 11 tasks (T094-T104)

### Parallel Opportunities:
- **13 parallel tasks** marked with [P] across all phases
- Setup phase: 5 parallel opportunities (T002-T006)
- Foundational phase: 8 parallel opportunities
- User stories: Multiple parallel opportunities within each story

### MVP Scope (Recommended):
- **Phase 1 + Phase 2 + User Story 1 (P1)** = 36 tasks
- Delivers: Bot can join/leave Discord voice channels via commands
- Estimated time: 1-2 weeks (1 developer)
- Provides foundation for all subsequent features

### Full Implementation:
- **All Phases**: 104 tasks
- Estimated time: 6 weeks (1 developer) or 3-4 weeks (2-3 developers in parallel)
- Delivers: Complete voice integration with AI conversations, function calling, cost management
