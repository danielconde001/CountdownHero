using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;


[InitializeOnLoad]
public static class DialogueTimelineClipRenamer
{
    static DialogueTimelineClipRenamer()
    {
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (TimelineEditor.inspectedDirector == null)
            return;

        TimelineAsset timeline = TimelineEditor.inspectedAsset;

        if (timeline == null)
            return;

        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is DialogueTimelineClip dialogueClip)
                    UpdateName(clip, dialogueClip);
            }
        }
    }

    private static void UpdateName(TimelineClip clip, DialogueTimelineClip dialogueClip)
    {
        if (clip == null || dialogueClip == null)
            return;

        DialogueSpeaker speaker = dialogueClip.speakerReference;
        string speakerName = (speaker != null) ? speaker.gameObject.name : "NULL";

        string text = dialogueClip.dialogueText;

        if (!string.IsNullOrEmpty(text))
        {
            text = text.Replace("\n", " ");

            if (text.Length > 30)
                text = text.Substring(0, 30) + "...";
        }
        else
            text = "<Empty>";

        string newName = $"{speakerName}: {text}";

        if (clip.displayName != newName)
        {
            clip.displayName = newName;
            EditorUtility.SetDirty(TimelineEditor.inspectedAsset);
        }
    }
}