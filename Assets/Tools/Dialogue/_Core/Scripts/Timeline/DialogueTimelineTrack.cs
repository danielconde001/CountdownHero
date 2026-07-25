using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


[DisplayName("Dialogue Track")]
[TrackClipType(typeof(DialogueTimelineClip))]
public class DialogueTimelineTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);

        clip.displayName = "New Dialogue Clip";

        clip.easeInDuration = 0.25;
        clip.easeOutDuration = 0.25;
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount)
    {
        return ScriptPlayable<DialogueTimelineMixer>.Create(graph, inputCount);
    }
}
