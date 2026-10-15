// Every controller vibration in the game, in one place
// Gameplay scripts only say WHAT happened (Haptics.HitTaken())
// Intensity is a global multiplier (0 = vibration off) that the settings menu will expose later.
public static class Haptics
{
    // Global strength multiplier, 0..1. 0 turns all vibration off.
    public static float Intensity = 1f;

    // The officer took a blow: strong buzz on BOTH controllers
    public static void HitTaken()
    {
        Pulse(Hand.Left, 1f, 0.25f);
        Pulse(Hand.Right, 1f, 0.25f);
    }

    // A blow was stopped by the shield: short medium kick only in the hand holding the shield, so the trainee feels the blow landed - on the shield arm
    public static void ShieldBlock(Hand hand)
    {
        Pulse(hand, 0.6f, 0.1f);
    }

    // The one place that applies Intensity. Every preset goes through here, so the global slider can never be forgotten in a new preset
    static void Pulse(Hand hand, float amplitude, float duration)
    {
        if (Intensity <= 0f)
            return;
        ControllerInput.Haptic(hand, amplitude * Intensity, duration);
    }
}
