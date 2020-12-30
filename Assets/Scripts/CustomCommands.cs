using PixelCrushers.DialogueSystem;
using Console;
using UnityEngine;

public class CustomCommands
{
    [ConsoleCommand("goto", "Change scene without breaking dialouge")]
    class GoTo : Command
    {
        [CommandParameter("scene to go to")]
        public string nextScene;

        public override ConsoleOutput Logic()
        {
            DialogueLua.SetVariable("Next_Scene", nextScene);
            DialogueManager.StopConversation();
            return new ConsoleOutput(nextScene, ConsoleOutput.OutputType.Log);
        }
    }

    [ConsoleCommand("setint", "Set a Lua database int variable")]
    class  SetInt : Command
    {
        [CommandParameter("Variable to change")]
        public string luaVariable;

        [CommandParameter("Target value")]
        public int targetValue;

        public override ConsoleOutput Logic()
        {
            DialogueLua.SetVariable(luaVariable, targetValue);
            return new ConsoleOutput("Value set", ConsoleOutput.OutputType.Log);
        }
    }

    [ConsoleCommand("setstring", "Set a Lua database int variable")]
    class SetString : Command
    {
        [CommandParameter("Variable to change")]
        public string luaVariable;

        [CommandParameter("Target value")]
        public string targetValue;

        public override ConsoleOutput Logic()
        {
            DialogueLua.SetVariable(luaVariable, targetValue);
            return new ConsoleOutput("Value set", ConsoleOutput.OutputType.Log);
        }
    }
}