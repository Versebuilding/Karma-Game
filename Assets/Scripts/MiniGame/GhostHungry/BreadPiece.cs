public class BreadPiece : Projectile
{
    protected override bool ResetConditionsMet() {
        if (transform.position.y < -1) {
            return true;
        }

        return false;
    }
}