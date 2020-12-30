// This version of the Ink integration example scene has these changes:
// 1. External Dialogue System functions, with fallbacks defined at the end of this file.
// 2. Actor=PlayerSpeaker tags to indicate the speaker where it's otherwise ambiguous.
// 3. Defines a variable wagerAmount and sets it to 20000 during the story.
//    You can set a watch in the Dialogue Editor to see it change.


EXTERNAL ShowAlert(x)                 // ShowAlert("message")
EXTERNAL CurrentQuestState(x)         // CurrentQuestState("quest")
EXTERNAL CurrentQuestEntryState(x,y)  // CurrentQuestEntryState("quest", entry#)
EXTERNAL SetQuestState(x,y)           // SetQuestState("quest", "inactive|active|success|failure")
EXTERNAL SetQuestEntryState(x,y,z)    // SetQuestEntryState("quest", entry#, "inactive|active|success|failure")

VAR wagerAmount = 0

- I looked at Monsieur Fogg  # Actor=Player
*   ... and I could contain myself no longer.
    'What is the purpose of our journey, Monsieur?' # Actor=Player
    'A wager,' he replied. { SetQuestState("The Wager", "active") } { ShowAlert("Quest: The Wager") }
    * *     'A wager!'[] I returned.
            He nodded. 
            * * *   'But surely that is foolishness!'
            * * *  'A most serious matter then!'
            - - -   He nodded again.
            * * *   'But can we win?'
                    'That is what we will endeavour to find out,' he answered.
            * * *   'A modest wager, I trust?'
                    'Twenty thousand pounds,' he replied, quite flatly.
					~ wagerAmount = 20000
            * * *   I asked nothing further of him then[.], and after a final, polite cough, he offered nothing more to me. <>
    * *     'Ah[.'],' I replied, uncertain what I thought.
    - -     After that, <>
*   ... but I said nothing[] and <>
- we passed the day in silence.
- -> END

// Fallback functions

== function ShowAlert(x) ==
~ return 1

== function CurrentQuestState(x) ==
~ return "inactive"

== function CurrentQuestEntryState(x,y) ==
~ return "inactive"

== function SetQuestState(x,y) ==
~ return 1

== function SetQuestEntryState(x,y,z) ==
~ return 1
