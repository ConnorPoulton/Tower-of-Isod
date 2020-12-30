using UnityEngine;
using System.Collections.Generic;
using Ink.Runtime;
using System.Collections;

namespace PixelCrushers.DialogueSystem.InkSupport
{

    /// <summary>
    /// Integrates Ink with the Dialogue System. In this integration, Ink does the
    /// processing, and the Dialogue System does the UI and handles triggers. It
    /// also handles saving/loading and exposes functions to manage quests and
    /// show alerts.
    /// </summary>
    [AddComponentMenu("Pixel Crushers/Dialogue System/Third Party Support/Ink/Dialogue System Ink Integration")]
    public class DialogueSystemInkIntegration : MonoBehaviour
    {

        [Tooltip("All Ink stories.")]
        public List<TextAsset> inkJSONAssets = new List<TextAsset>();

        [Tooltip("Reset story state when conversation starts.")]
        public bool resetStateOnConversationStart = false;

        [Tooltip("Reset story state when conversation starts.")]
        public bool includeInSaveData = true;

        [Tooltip("Actor names precede text, as in 'Monsieur Fogg: A wager.'")]
        public bool actorNamesPrecedeLines = false;

        [Tooltip("Trim whitespace from the beginning and end of lines.")]
        public bool trimText = true;

        [Tooltip("Append a line feed at the end of player response subtitles.")]
        public bool appendNewlineToPlayerResponses = false;

        [Tooltip("When player chooses response from menu, pull next line from story to show as response's subtitle.")]
        public bool playerDialogueTextFollowsResponseText = false;

        [Tooltip("When a line in Ink has a {Sequence()} function, tie it to the timing of the subtitle.")]
        public bool tieSequencesToDialogueEntries = true;

        // All loaded stories:
        public List<Story> stories { get; set; }

        // Currently-playing story:
        public Story activeStory { get; protected set; }

        public DialogueDatabase database { get; protected set; }

        // Template used to create new Dialogue System assets on the fly:
        protected Template template;

        protected const int InkConversationID = 999;
        protected const int PlayerActorID = 1;
        protected const int StoryActorID = 2; // e.g. NPC

        protected Conversation inkConversation;
        protected DialogueEntry inkStoryEntry;
        protected DialogueEntry resumeEntry;
        protected int nextStoryConversationID;
        protected bool isResuming = false; // Temp variable to know if we're resuming from a saved game.
        protected bool isPlayerSpeaking = false; // Temp variable to remember if next line is the expanded player choice text.
        protected int currentStoryPlayerID; // Conversation actor for current active story; defaults to PlayerActorID.
        protected int currentStoryActorID; // Conversation conversant for current active story; defaults to StoryActorID.
        protected string jumpToKnot = string.Empty; // If set, jump to this knot.
        protected string sequenceToPlayWithSubtitle = string.Empty; // May be set by {Sequence} function.

        protected static DialogueSystemInkIntegration m_instance = null;
        protected static bool m_registeredLuaFunctions = false;

        public static DialogueSystemInkIntegration instance { get { return m_instance; } }

        /// <summary>
        /// Knot that conversation started at, or blank if started at the beginning of the story.
        /// </summary>
        public static string lastStartingPoint { get; set; }

        #region Initialization

        protected virtual void Awake()
        {
            m_instance = this;
            database = null;
            RegisterLuaFunctions();
        }

        protected virtual void OnEnable()
        {
            if (includeInSaveData)
            {
                PersistentDataManager.RegisterPersistentData(this.gameObject);
            }
        }

        protected virtual void OnDisable()
        {
            if (includeInSaveData)
            {
                PersistentDataManager.UnregisterPersistentData(this.gameObject);
            }
        }

        protected virtual void Start()
        {
            CreateDatabase();

            // Observe Ink variables:
            foreach (var story in stories)
            {
                ObserveStoryVariables(story);
            }
        }

        protected virtual void CreateDatabase()
        {
            // Create database that will hold the stories:
            database = ScriptableObject.CreateInstance<DialogueDatabase>();
            database.name = "Ink";
            template = Template.FromDefault();

            // Default actors (in case initial database doesn't define these):            
            var playerActor = CreateActor(template, PlayerActorID, "Player", true);
            database.actors.Add(playerActor);
            var npcActor = CreateActor(template, StoryActorID, "NPC", false);
            database.actors.Add(npcActor);
            //var playerSpeakerActor = CreateActor(template, PlayerSpeakerActorID, "Player", false);
            //database.actors.Add(playerSpeakerActor);

            // Fake conversation for Ink:
            inkConversation = template.CreateConversation(InkConversationID, "Ink");
            inkConversation.ActorID = PlayerActorID;
            inkConversation.ConversantID = StoryActorID;

            // Start entry:
            var startEntry = template.CreateDialogueEntry(0, InkConversationID, "START");
            startEntry.ActorID = PlayerActorID;// PlayerSpeakerActorID;
            startEntry.ConversantID = StoryActorID;
            startEntry.Sequence = "None()";
            inkConversation.dialogueEntries.Add(startEntry);

            // Fake entry:
            inkStoryEntry = template.CreateDialogueEntry(1, InkConversationID, "Story");
            inkStoryEntry.ActorID = StoryActorID;
            inkStoryEntry.ConversantID = PlayerActorID; // PlayerSpeakerActorID;
            inkConversation.dialogueEntries.Add(inkStoryEntry);
            // Start --> fake entry:
            startEntry.outgoingLinks.Add(new Link(InkConversationID, 0, InkConversationID, 1));

            // Add fake conversation to database:
            database.conversations.Add(inkConversation);

            // Load each story from JSON and add to database as a stub conversation:
            stories = new List<Story>();
            activeStory = null;
            nextStoryConversationID = InkConversationID + 1;
            for (int i = 0; i < inkJSONAssets.Count; i++)
            {
                AddStoryToDatabase(inkJSONAssets[i]);
            }

            // Add database:
            DialogueManager.AddDatabase(database);
        }

        protected virtual Actor CreateActor(Template template, int actorID, string actorName, bool isPlayer)
        {
            Actor actor;
            var existingPlayerActor = DialogueManager.masterDatabase.GetActor(PlayerActorID);
            if (existingPlayerActor != null)
            {
                actor = new Actor(existingPlayerActor);
                actor.fields = Field.CopyFields(existingPlayerActor.fields);
            }
            else
            {
                actor = template.CreateActor(actorID, actorName, isPlayer);
            }
            return actor;
        }

        protected virtual void AddStoryToDatabase(TextAsset asset)
        {
            if (DialogueDebug.LogInfo) Debug.Log("Dialogue System: Loading Ink story " + asset.name);
            var story = new Story(asset.text);
            stories.Add(story);

            // Create conversation that links straight to fake Ink conversation:
            var conversation = template.CreateConversation(nextStoryConversationID, asset.name);
            nextStoryConversationID++;
            conversation.ActorID = PlayerActorID;
            conversation.ConversantID = StoryActorID;
            var startEntry = template.CreateDialogueEntry(0, conversation.id, "START");
            startEntry.ActorID = PlayerActorID;
            startEntry.ConversantID = StoryActorID;
            startEntry.Sequence = "None()";
            startEntry.outgoingLinks.Add(new Link(conversation.id, 0, InkConversationID, 1));
            conversation.dialogueEntries.Add(startEntry);
            database.conversations.Add(conversation);

            // Add story variables to database:
            var variables = story.variablesState;
            int variableID = 1;
            foreach (var variableName in variables)
            {
                if (database.GetVariable(variableName) != null) continue;
                var variable = template.CreateVariable(variableID++, variableName, string.Empty);
                SetVariableValue(variable, variables[variableName]);
                database.variables.Add(variable);
            }

            // Register external functions:
            BindExternalFunctions(story);
        }

        /// <summary>
        /// Adds a story at runtime.
        /// </summary>
        /// <param name="storyTitle">The story's title.</param>
        /// <param name="storyText">The story in JSON format.</param>
        public virtual void AddStory(string storyTitle, string storyJSON)
        {
            if (string.IsNullOrEmpty(storyJSON)) return;
            if (DialogueDebug.LogInfo) Debug.Log("Dialogue System: Loading Ink story " + storyTitle);
            var story = new Story(storyJSON);
            stories.Add(story);

            // Create conversation that links straight to fake Ink conversation:
            var conversation = template.CreateConversation(nextStoryConversationID, storyTitle);
            nextStoryConversationID++;
            conversation.ActorID = PlayerActorID;
            conversation.ConversantID = StoryActorID;
            var startEntry = template.CreateDialogueEntry(0, conversation.id, "START");
            startEntry.ActorID = PlayerActorID;
            conversation.ConversantID = StoryActorID;
            startEntry.Sequence = "None()";
            startEntry.outgoingLinks.Add(new Link(conversation.id, 0, InkConversationID, 0));
            conversation.dialogueEntries.Add(startEntry);
            database.conversations.Add(conversation);
            var conversationList = new List<Conversation>();
            conversationList.Add(conversation);
            DialogueLua.AddToConversationTable(conversationList, new List<DialogueDatabase>());

            // Add story variables to database:
            var variables = story.variablesState;
            int variableID = 1;
            foreach (var variableName in variables)
            {
                if (database.GetVariable(variableName) != null) continue;
                var variable = template.CreateVariable(variableID++, variableName, string.Empty);
                SetVariableValue(variable, variables[variableName]);
                database.variables.Add(variable);
                DialogueLua.SetVariable(variableName, variables[variableName]);
            }

            // Register external functions:
            BindExternalFunctions(story);
        }

        #endregion

        #region Variables

        protected virtual void SetVariableValue(Variable variable, object value)
        {
            var initialValue = Field.Lookup(variable.fields, "Initial Value");
            if (initialValue == null) return;
            initialValue.value = (value != null) ? value.ToString() : string.Empty;
            initialValue.type = GetFieldType(value);
        }

        protected FieldType GetFieldType(object value)
        {
            if (value == null) return FieldType.Text;
            var type = value.GetType();
            if (type == typeof(bool)) return FieldType.Boolean;
            if (type == typeof(int) || type == typeof(float) || type == typeof(double)) return FieldType.Number;
            return FieldType.Text;
        }

        protected virtual void ObserveStoryVariables(Story story)
        {
            foreach (var variableName in story.variablesState)
            {
                story.ObserveVariable(variableName, OnVariableChange);
            }
        }

        protected virtual void OnVariableChange(string variableName, object newValue)
        {
            if (DialogueDebug.LogInfo) Debug.Log("Dialogue System: Ink variable '" + variableName + "' changed to " + newValue);
            DialogueLua.SetVariable(variableName, newValue);
        }

        #endregion

        #region Handle active conversation

        public static void SetConversationStartingPoint(string knot)
        {
            if (m_instance != null)
            {
                m_instance.jumpToKnot = knot;
                lastStartingPoint = knot;
            }
        }

        public static Story LookupStory(string storyName)
        {
            if (instance == null) return null;
            for (int i = 0; i < instance.inkJSONAssets.Count; i++)
            {
                if (string.Equals(instance.inkJSONAssets[i].name, storyName))
                {
                    return instance.stories[i];
                }
            }
            return null;
        }

        protected virtual void OnConversationStart(Transform actorTransform)
        {
            for (int i = 0; i < inkJSONAssets.Count; i++)
            {
                if (string.Equals(inkJSONAssets[i].name, DialogueManager.LastConversationStarted))
                {
                    activeStory = stories[i];
                    if (resetStateOnConversationStart) activeStory.ResetState();

                    // Get current player ID and NPC ID:
                    var actor = DialogueManager.masterDatabase.GetActor(DialogueActor.GetActorName(actorTransform));
                    currentStoryPlayerID = (actor != null) ? actor.id : PlayerActorID;
                    var conversant = DialogueManager.masterDatabase.GetActor(DialogueActor.GetActorName(DialogueManager.currentConversant));
                    currentStoryActorID = (conversant != null) ? conversant.id : StoryActorID;

                    return;
                }
            }
            activeStory = null;
        }

        protected virtual void OnConversationEnd(Transform actor)
        {
            activeStory = null;
        }

        protected virtual void OnPrepareConversationLine(DialogueEntry entry)
        {
            if (entry.id == 0 || activeStory == null || entry.conversationID != InkConversationID)
            {
                // START entry or not the fake Ink conversation: Do nothing special.
            }
            else if (entry.id == 1)
            {
                // If jump is specified, jump there:
                if (!string.IsNullOrEmpty(jumpToKnot))
                {
                    activeStory.ChoosePathString(jumpToKnot);
                    jumpToKnot = string.Empty;
                }

                // Story text entry: Show next story text, and add responses if available.
                entry.outgoingLinks.Clear();

                if (activeStory.canContinue || isResuming)
                {
                    var text = isResuming ? activeStory.currentText : activeStory.Continue();
                    if (trimText) text = text.Trim();
                    entry.ActorID = (isResuming && isPlayerSpeaking) ? currentStoryPlayerID : currentStoryActorID;
                    entry.ConversantID = isPlayerSpeaking ? currentStoryActorID : currentStoryPlayerID;
                    isResuming = false;
                    isPlayerSpeaking = false;

                    if (actorNamesPrecedeLines) TryExtractPrependedActor(ref text, entry);
                    foreach (var tag in activeStory.currentTags)
                    {
                        if (tag.StartsWith("Actor=")) entry.ActorID = GetActorID(tag.Substring("Actor=".Length), entry.ActorID);
                        else if (tag.StartsWith("Conversant=")) entry.ConversantID = GetActorID(tag.Substring("Conversant=".Length), entry.ConversantID);
                    }
                    entry.DialogueText = text;
                    entry.Sequence = string.Empty;
                }
                // And prepare outgoing links:
                inkConversation.dialogueEntries.RemoveRange(2, inkConversation.dialogueEntries.Count - 2);
                if (activeStory.canContinue)
                {
                    // Add linkback to entry 1 for more text:
                    entry.outgoingLinks.Add(new Link(inkConversation.id, entry.id, inkConversation.id, entry.id));
                }
                else
                {
                    // No more text, so add responses:
                    AddResponses(entry);
                }
            }
            else
            {
                // Choice entry: Choose choice.
                activeStory.ChooseChoiceIndex(Field.LookupInt(entry.fields, "Choice Index"));
                entry.id = 1;
                if (playerDialogueTextFollowsResponseText)
                {
                    if (activeStory.canContinue) activeStory.Continue();
                    entry.DialogueText = activeStory.currentText;

                }
                if (!activeStory.canContinue)
                {
                    entry.outgoingLinks.Clear();
                    AddResponses(entry);
                }
                var text = entry.subtitleText;
                if (trimText) text = text.Trim();
                if (appendNewlineToPlayerResponses) text += "\n";
                TryExtractPrependedActor(ref text, entry);
                entry.DialogueText = text;
                isPlayerSpeaking = true;
            }
        }

        protected void AddResponses(DialogueEntry entry)
        {
            for (int i = 0; i < activeStory.currentChoices.Count; i++)
            {
                Choice choice = activeStory.currentChoices[i];
                var choiceText = choice.text;
                if (trimText) choiceText = choiceText.Trim();
                var responseEntry = template.CreateDialogueEntry(2 + i, inkConversation.id, "Choice " + i);
                responseEntry.ActorID = currentStoryPlayerID;
                responseEntry.ConversantID = currentStoryActorID;
                if (actorNamesPrecedeLines) TryExtractPrependedActor(ref choiceText, responseEntry);
                responseEntry.MenuText = choiceText;
                if (DialogueManager.DisplaySettings.subtitleSettings.skipPCSubtitleAfterResponseMenu)
                {
                    responseEntry.Sequence = "None()";
                }
                responseEntry.DialogueText = string.Empty;
                Field.SetValue(responseEntry.fields, "Choice Index", i);
                responseEntry.outgoingLinks.Add(new Link(inkConversation.id, responseEntry.id, inkConversation.id, entry.id));
                inkConversation.dialogueEntries.Add(responseEntry);
                entry.outgoingLinks.Add(new Link(inkConversation.id, entry.id, inkConversation.id, responseEntry.id));
            }

        }

        // Extract actor ID from 'Actor: Text'.
        protected void TryExtractPrependedActor(ref string text, DialogueEntry entry)
        {
            if (text != null && text.Contains(":"))
            {
                var colonPos = text.IndexOf(':');
                if (colonPos < text.Length - 1)
                {
                    var actorName = text.Substring(0, colonPos);
                    text = text.Substring(colonPos + 1).TrimStart();
                    var actor = DialogueManager.MasterDatabase.GetActor(actorName);
                    if (actor != null)
                    {
                        if (entry.ConversantID == actor.id)
                        { // If conversant points to new actor (speaker), point it to the other participant.
                            entry.ConversantID = entry.ActorID;
                        }

                        entry.ActorID = actor.id;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to extract an actor name from the beginning of a string.
        /// </summary>
        /// <param name="text">Text possibly containing an actor name and colon at the beginning.</param>
        /// <param name="actorName">Actor name (if extracted).</param>
        /// <returns>True if an actor name was extracted; false otherwise.</returns>
        public static bool TryExtractPrependedActor(ref string text, out string actorName)
        {
            if (text != null && text.Contains(":"))
            {
                var colonPos = text.IndexOf(':');
                if (colonPos < text.Length - 1)
                {
                    actorName = text.Substring(0, colonPos);
                    text = text.Substring(colonPos + 1).TrimStart();
                    return true;
                }
            }
            actorName = string.Empty;
            return false;
        }

        protected int GetActorID(string actorName, int defaultID)
        {
            var actor = DialogueManager.MasterDatabase.GetActor(actorName);
            return (actor != null) ? actor.id : defaultID;
        }

        protected virtual void OnConversationLine(Subtitle subtitle)
        {
            if (string.IsNullOrEmpty(sequenceToPlayWithSubtitle) &&
                (subtitle.sequence == "None()" || subtitle.sequence == "Continue()"))
            {
                return;
            }
            subtitle.sequence = sequenceToPlayWithSubtitle;
            sequenceToPlayWithSubtitle = string.Empty;
        }

        #endregion

        #region External Functions

        protected virtual void BindExternalFunctions(Story story)
        {
            if (story == null) return;
            story.BindExternalFunction("ShowAlert", (string message) => { DialogueManager.ShowAlert(message); });
            story.BindExternalFunction("Sequence", (string sequence) => { PlaySequenceFromInk(sequence); });
            story.BindExternalFunction("CurrentQuestState", (string questName) => { return QuestLog.CurrentQuestState(questName); });
            story.BindExternalFunction("CurrentQuestEntryState", (string questName, int entryNumber) => { return QuestLog.CurrentQuestEntryState(questName, entryNumber); });
            story.BindExternalFunction("SetQuestState", (string questName, string state) => { QuestLog.SetQuestState(questName, state); });
            story.BindExternalFunction("SetQuestEntryState", (string questName, int entryNumber, string state) => { QuestLog.SetQuestEntryState(questName, entryNumber, state); });
            story.BindExternalFunction("GetBoolVariable", (string variableName) => { return DialogueLua.GetVariable(variableName).asBool; });
            story.BindExternalFunction("GetIntVariable", (string variableName) => { return DialogueLua.GetVariable(variableName).asInt; });
            story.BindExternalFunction("GetStringVariable", (string variableName) => { return DialogueLua.GetVariable(variableName).asString; });
            story.BindExternalFunction("SetBoolVariable", (string variableName, bool value) => { DialogueLua.SetVariable(variableName, value); });
            story.BindExternalFunction("SetIntVariable", (string variableName, int value) => { DialogueLua.SetVariable(variableName, value); });
            story.BindExternalFunction("SetStringVariable", (string variableName, string value) => { DialogueLua.SetVariable(variableName, value); });
        }

        protected virtual void PlaySequenceFromInk(string sequence)
        {
            if (DialogueManager.isConversationActive && tieSequencesToDialogueEntries)
            {
                if (!string.IsNullOrEmpty(sequenceToPlayWithSubtitle)) sequenceToPlayWithSubtitle += "; ";
                sequenceToPlayWithSubtitle += sequence;
            }
            else
            {
                StartCoroutine(PlaySequenceAtEndOfFrame(sequence));
            }

        }

        IEnumerator PlaySequenceAtEndOfFrame(string sequence)
        {
            // Must wait for end of frame for conversation state to be fully updated.
            yield return new WaitForEndOfFrame();
            Transform barker = null;
            Transform listener = null;
            if (DialogueManager.currentConversationState != null)
            {
                barker = DialogueManager.currentConversationState.subtitle.speakerInfo.transform;
                listener = DialogueManager.currentConversationState.subtitle.listenerInfo.transform;
            }
            DialogueManager.PlaySequence(sequence, barker, listener);
        }

        protected virtual void UnbindExternalFunctions(Story story)
        {
            if (story == null) return;
            story.UnbindExternalFunction("ShowAlert");
            story.UnbindExternalFunction("Sequence");
            story.UnbindExternalFunction("CurrentQuestState");
            story.UnbindExternalFunction("CurrentQuestEntryState");
            story.UnbindExternalFunction("SetQuestState");
            story.UnbindExternalFunction("SetQuestEntryState");
            story.UnbindExternalFunction("GetBoolVariable");
            story.UnbindExternalFunction("GetIntVariable");
            story.UnbindExternalFunction("GetStringVariable");
            story.UnbindExternalFunction("SetBoolVariable");
            story.UnbindExternalFunction("SetIntVariable");
            story.UnbindExternalFunction("SetStringVariable");
        }

        #endregion

        #region Lua Functions

        protected virtual void RegisterLuaFunctions()
        {
            if (m_registeredLuaFunctions) return;
            m_registeredLuaFunctions = true;
            Lua.RegisterFunction("SetInkBool", null, SymbolExtensions.GetMethodInfo(() => SetInkBool(string.Empty, false)));
            Lua.RegisterFunction("SetInkNumber", null, SymbolExtensions.GetMethodInfo(() => SetInkNumber(string.Empty, (double)0)));
            Lua.RegisterFunction("SetInkString", null, SymbolExtensions.GetMethodInfo(() => SetInkString(string.Empty, string.Empty)));
            Lua.RegisterFunction("GetInkBool", null, SymbolExtensions.GetMethodInfo(() => GetInkBool(string.Empty)));
            Lua.RegisterFunction("GetInkNumber", null, SymbolExtensions.GetMethodInfo(() => GetInkNumber(string.Empty)));
            Lua.RegisterFunction("GetInkString", null, SymbolExtensions.GetMethodInfo(() => GetInkString(string.Empty)));
        }

        public static void SetInkBool(string variableName, bool value)
        {
            if (m_instance != null) m_instance.SetInkVariableValue(variableName, value);
        }

        public static void SetInkNumber(string variableName, double value)
        {
            if (m_instance != null) m_instance.SetInkVariableValue(variableName, (float)value);
        }

        public static void SetInkString(string variableName, string value)
        {
            if (m_instance != null) m_instance.SetInkVariableValue(variableName, value);
        }

        public static bool GetInkBool(string variableName)
        {
            if (m_instance == null) return false;
            var value = m_instance.GetInkVariableValue(variableName);
            if (value == null)
            {
                return false;
            }
            else if (value.GetType() == typeof(bool))
            {
                return (bool)value;
            }
            else
            {
                return Tools.StringToBool(value.ToString());
            }
        }

        public static double GetInkNumber(string variableName)
        {
            if (m_instance == null) return 0;
            var value = m_instance.GetInkVariableValue(variableName);
            if (value == null) return 0;
            else if (value.GetType() == typeof(float)) return (float)value;
            else if (value.GetType() == typeof(int)) return (int)value;
            else if (value.GetType() == typeof(double)) return (double)value;
            else return Tools.StringToFloat(value.ToString());
        }

        public static string GetInkString(string variableName)
        {
            if (m_instance == null) return string.Empty;
            var value = m_instance.GetInkVariableValue(variableName);
            return (value != null) ? value.ToString() : string.Empty;
        }

        protected virtual void SetInkVariableValue(string variableName, object value)
        {
            foreach (var story in stories)
            {
                var storyContainsVariable = false;
                foreach (var variable in story.variablesState)
                {
                    if (string.Equals(variable, variableName))
                    {
                        storyContainsVariable = true;
                        break;
                    }
                }
                if (storyContainsVariable) story.variablesState[variableName] = value;
            }
        }

        protected virtual object GetInkVariableValue(string variableName)
        {
            foreach (var story in stories)
            {
                var storyContainsVariable = false;
                foreach (var variable in story.variablesState)
                {
                    if (string.Equals(variable, variableName))
                    {
                        storyContainsVariable = true;
                        break;
                    }
                }
                if (storyContainsVariable) return story.variablesState[variableName];
            }
            return null;
        }

        #endregion

        #region Save

        public virtual void ResetStories()
        {
            stories.ForEach(story => story.ResetState());
        }

        protected virtual void OnLevelWillBeUnloaded()
        {
            if (includeInSaveData)
            {
                if (GetComponent<DialogueSystemSaver>() != null)
                {
                    DialogueManager.RemoveDatabase(database);
                }
            }
        }

        protected virtual void OnRecordPersistentData()
        {
            if (!includeInSaveData) return;
            var currentStory = string.Empty;
            for (int i = 0; i < inkJSONAssets.Count; i++)
            {
                var varName = "Story_" + inkJSONAssets[i].name;
                var story = stories[i];
                var json = story.state.ToJson();
                DialogueLua.SetVariable(varName, json);
                if (DialogueManager.isConversationActive && string.Equals(inkJSONAssets[i].name, DialogueManager.lastConversationStarted))
                {
                    currentStory = inkJSONAssets[i].name;
                }
            }
            if (DialogueDebug.logInfo) Debug.Log("Dialogue System: Recording that active story is '" + currentStory + "' (isPlayerSpeaking=" + isPlayerSpeaking + ").");
            DialogueLua.SetVariable("CurrentStory", currentStory);
            DialogueLua.SetVariable("WasPlayerSpeaking", isPlayerSpeaking);
        }

        protected virtual void OnApplyPersistentData()
        {
            if (!includeInSaveData) return;
            DialogueManager.AddDatabase(database);
            ResetStories();
            for (int i = 0; i < inkJSONAssets.Count; i++)
            {
                var varName = "Story_" + inkJSONAssets[i].name;
                if (DialogueLua.DoesVariableExist(varName))
                {
                    var json = DialogueLua.GetVariable(varName).AsString;
                    if (DialogueDebug.logInfo) Debug.Log("Dialogue System: Restoring story '" + inkJSONAssets[i].name + "' state: " + json);
                    stories[i].state.LoadJson(json);
                }
            }
            DialogueManager.StopConversation();

            var currentStory = DialogueLua.GetVariable("CurrentStory").asString;
            if (!string.IsNullOrEmpty(currentStory))
            {
                isResuming = true;
                isPlayerSpeaking = DialogueLua.GetVariable("WasPlayerSpeaking").asBool;
                if (DialogueDebug.logInfo) Debug.Log("Dialogue System: Resuming story '" + currentStory + "' (wasPlayerSpeaking=" + isPlayerSpeaking + ").");
                DialogueManager.AddDatabase(database);
                DialogueManager.StartConversation(currentStory);
            }
        }

        #endregion

        #region Get Actors In Story

        public virtual Story GetStory(string storyName)
        {
            for (int i = 0; i < inkJSONAssets.Count; i++)
            {
                if (string.Equals(inkJSONAssets[i].name, storyName))
                {
                    return new Story(inkJSONAssets[i].text);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the names of all actors in the story or knot.
        /// </summary>
        /// <param name="storyName">Story name.</param>
        /// <param name="knotName">Knot name, or blank for entire story.</param>
        public string[] GetActorsInStory(string storyName, string knotName)
        {
            var actors = new List<string>();

            var story = GetStory(storyName);
            if (story == null) return actors.ToArray();
            story.ResetState();

            if (!string.IsNullOrEmpty(knotName))
            {
                story.ChoosePathString(knotName);
            }

            var visitedChoices = new HashSet<Choice>();
            var knotPath = string.IsNullOrEmpty(knotName) ? string.Empty : knotName + ".";
            RecordActorsInStoryRecursive(story, knotPath, visitedChoices, actors, 0);

            return actors.ToArray();
        }

        protected void RecordActorsInStoryRecursive(Story story, string knotPath, HashSet<Choice> visitedChoices, List<string> actors, int recursionDepth)
        {
            if (recursionDepth > 1024) return; // Safeguard to prevent infinite recursion.

            int safeguard = 0;
            bool hasMoreText;

            // Process all lines until we get to a choice:
            do {
                // Check actor tags:
                foreach (var tag in story.currentTags)
                {
                    if (tag.StartsWith("Actor=")) RecordActor(actors, tag.Substring("Actor=".Length));
                    else if (tag.StartsWith("Conversant=")) RecordActor(actors, tag.Substring("Conversant=".Length));
                }

                // Check prepended actor names:
                if (actorNamesPrecedeLines)
                {
                    var text = story.currentText;
                    if (text != null && text.Contains(":"))
                    {
                        var colonPos = text.IndexOf(':');
                        if (colonPos < text.Length - 1)
                        {
                            RecordActor(actors, text.Substring(0, colonPos));
                        }
                    }
                }

                hasMoreText = story.canContinue;
                if (story.canContinue)
                {
                    story.Continue();
                }
            } while (hasMoreText && ++safeguard < 16348);

            // Process all choices recursively:
            var numChoices = story.currentChoices.Count;
            if (numChoices > 0)
            {
                var savedState = story.state.ToJson();
                var knotOnly = !string.IsNullOrEmpty(knotPath);
                for (int i = 0; i < numChoices; i++)
                {
                    var choice = story.currentChoices[i];
                    if (visitedChoices.Contains(choice)) continue;
                    if (knotOnly && !choice.sourcePath.StartsWith(knotPath)) continue;
                    visitedChoices.Add(choice);
                    story.ChooseChoiceIndex(i);
                    RecordActorsInStoryRecursive(story, knotPath, visitedChoices, actors, recursionDepth + 1);
                    story.state.LoadJson(savedState);
                }
            }
        }

        protected void RecordActor(List<string> actors, string actorName)
        {
            if (string.IsNullOrEmpty(actorName) || actors.Contains(actorName)) return;
            actors.Add(actorName);
        }

        #endregion

    }
}
