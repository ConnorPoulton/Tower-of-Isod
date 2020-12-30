using System;

namespace PixelCrushers.DialogueSystem.InkSupport
{

    public class InkEntrypoint
    {
        public string story;
        public string knot;
        public string stitch;

        public InkEntrypoint(string story, string knot, string stitch)
        {
            this.story = story;
            this.knot = knot;
            this.stitch = stitch;
        }

        /// <summary>
        /// Converts a string of the format "story/knot.stitch" into an InkEntrypoint object.
        /// You can omit knot and stitch -- e.g., "story/knot" or "story".
        /// </summary>
        public static InkEntrypoint FromString(string s)
        {
            string story, knot, stitch;
            var i = s.IndexOf('/');
            if (i == -1)
            {
                story = s;
                knot = stitch = string.Empty;
            }
            else
            {
                story = s.Substring(0, i);
                var knot_stitch = s.Substring(i + 1);
                i = knot_stitch.IndexOf('.');
                if (i == -1)
                {
                    knot = knot_stitch;
                    stitch = string.Empty;
                }
                else
                {
                    knot = s.Substring(0, i);
                    stitch = s.Substring(i + 1);

                }
            }
            return new InkEntrypoint(story, knot, stitch);
        }

        /// <summary>
        /// Converts an InkEntrypoint in "story", "story/knot", or "story/knot.stitch".
        /// </summary>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(stitch))
            {
                if (string.IsNullOrEmpty(knot))
                {
                    return story;
                }
                else
                {
                    return story + "/" + knot;
                }
            }
            else
            {
                return story + "/" + knot + "." + stitch;
            }
        }

        // Used internally by custom editors.
        public string ToPopupString()
        {
            if (!string.IsNullOrEmpty(knot))
            {
                if (!string.IsNullOrEmpty(stitch)) return story.Replace("/", "-") + "/" + knot.Replace("/", "-") + "/" + stitch.Replace("/", "-");
                else return story + "/" + knot;
            }
            else
            {
                return story;
            }
        }

    }
}