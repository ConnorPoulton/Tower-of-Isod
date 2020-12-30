using Ink.Runtime;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.InkSupport
{

    public class DialogueSystemInkTrigger : DialogueSystemTrigger
    {
        [Tooltip("Jump to this knot. Can also specify 'knot.stitch' to jump to a stitch in the knot. If blank, start from the beginning.")]
        public string startConversationAtKnot;

        [Tooltip("Bark this knot. Can also specify 'knot.stitch' to jump to a stitch in the knot. If blank, bark from the beginning.")]
        public string barkKnot;

        protected override void DoConversationAction(Transform actor)
        {
            DialogueSystemInkIntegration.SetConversationStartingPoint(startConversationAtKnot);
            base.DoConversationAction(actor);
        }

        protected override void DoBarkAction(Transform actor)
        {
            // If not barking from a conversation, use default method:
            if (barkSource != BarkSource.Conversation)
            {
                base.DoBarkAction(actor);
                return;
            }

            // If barking from a conversation, manually get the story text and bark it:
            Story story = DialogueSystemInkIntegration.LookupStory(barkConversation);
            if (story == null) return;
            if (!string.IsNullOrEmpty(barkKnot))
            {
                story.ChoosePathString(barkKnot);
            }
            barkText = story.Continue().Trim();
            string actorName;
            if (DialogueSystemInkIntegration.TryExtractPrependedActor(ref barkText, out actorName))
            {
                barker = CharacterInfo.GetRegisteredActorTransform(actorName);
            }
            barkSource = BarkSource.Text;
            base.DoBarkAction(actor);
            barkSource = BarkSource.Conversation;
        }
    }
}
