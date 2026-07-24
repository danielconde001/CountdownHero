using UnityEngine.Playables;

public class DialogueTimelineMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();

        DialogueSpeaker speaker = null;
        string dialogueText = "";

        float alpha = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);

            if (weight <= 0)
                continue;

            ScriptPlayable<DialogueTimelineBehaviour> input = (ScriptPlayable<DialogueTimelineBehaviour>) playable.GetInput(i);

            DialogueTimelineBehaviour behaviour = input.GetBehaviour();

            speaker = behaviour.speaker;
            dialogueText = behaviour.dialogueText;

            // Timeline ease in/out value
            alpha = weight;
        }

        if (speaker != null)
            DialogueBubble.SetDialogue(speaker, dialogueText, alpha);
        else
            DialogueBubble.ClearDialogue();
    }


    public override void OnGraphStop(Playable playable)
    {
        DialogueBubble.ClearDialogue();
    }
}
