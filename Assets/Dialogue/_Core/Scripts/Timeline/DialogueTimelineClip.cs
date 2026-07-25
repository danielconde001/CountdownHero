using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor.Timeline;
#endif

public class DialogueTimelineClip : PlayableAsset
{
    public DialogueTimelineBehaviour template = new DialogueTimelineBehaviour();

    [SerializeField] private ExposedReference<DialogueSpeaker> _speaker;

    [TextArea]
    [SerializeField] private string _dialogueText;


    #if UNITY_EDITOR
    public DialogueSpeaker speakerReference
    {
        get
        {
            PlayableDirector director = TimelineEditor.inspectedDirector;
            if (director == null)
                return null;

            PlayableGraph graph = director.playableGraph;
            if (!graph.IsValid())
                return null;

            return _speaker.Resolve(graph.GetResolver());
        }
    }
    public string dialogueText => _dialogueText;
    #endif


    // Default clip capabilities
    public ClipCaps clipCaps => ClipCaps.Blending;


    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<DialogueTimelineBehaviour>.Create(graph);

        DialogueTimelineBehaviour behaviour = playable.GetBehaviour();

        behaviour.speaker = _speaker.Resolve(graph.GetResolver());
        behaviour.dialogueText = _dialogueText;

        return playable;
    }
}
