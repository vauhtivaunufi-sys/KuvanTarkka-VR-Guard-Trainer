// Every controller vibration in one place, gameplay only says what happened and Intensity scales them all
public static class Haptics
{
    // Global strength multiplier, 0..1. 0 turns all vibration off
    public static float Intensity = 1f;

    // The officer took a blow: strong buzz on BOTH controllers
    public static void HitTaken()
    {
        Pulse(Hand.Left, 1f, 0.25f);
        Pulse(Hand.Right, 1f, 0.25f);
    }

    // Blow stopped by the shield: short kick in the shield hand
    public static void ShieldBlock(Hand hand)
    {
        Pulse(hand, 0.6f, 0.1f);
    }

    // Taser safety switched: a light click
    public static void TaserSafety(Hand hand)
    {
        Pulse(hand, 0.25f, 0.04f);
    }

    // Taser fired: strong and short, like the kick of the cartridge
    public static void TaserFire(Hand hand)
    {
        Pulse(hand, 0.9f, 0.12f);
    }

    // Current running through the wires: a medium shiver for the whole cycle
    public static void TaserCycle(Hand hand, float seconds)
    {
        Pulse(hand, 0.4f, seconds);
    }

    // Warning arc crackling at the muzzle
    public static void TaserArc(Hand hand)
    {
        Pulse(hand, 0.5f, 0.3f);
    }

    // Trigger pulled with no cartridges left: barely there
    public static void TaserDryFire(Hand hand)
    {
        Pulse(hand, 0.08f, 0.03f);
    }

    // Command menu moved to another command: a tiny tick, like the notch of a dial
    public static void MenuTick(Hand hand)
    {
        Pulse(hand, 0.2f, 0.03f);
    }

    // The only place that applies Intensity, so no preset can skip it
    static void Pulse(Hand hand, float amplitude, float duration)
    {
        if (Intensity <= 0f)
            return;
        ControllerInput.Haptic(hand, amplitude * Intensity, duration);
    }
}
