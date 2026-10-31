namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// E를 짧게가 아니라 꾹 눌러야 하는 손 동작 대상 (기획서 v2.0 · 주요 변동사항: 수확 = E 꾹 0.5초 + 진행 고리).
    /// IHandInteractable과 같은 오브젝트에 둔다. 누르는 시간은 PlayerSettings(HandHoldSeconds).
    /// </summary>
    public interface IHandHoldTarget
    {
        /// <summary>지금 E 동작이 꾹 누르기인지 (수확할 수 있을 때 등).</summary>
        bool HandNeedsHold { get; }
    }
}
