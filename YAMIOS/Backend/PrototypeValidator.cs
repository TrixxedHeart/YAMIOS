using YAMIOS.Stuff;

namespace YAMIOS.Backend
{
    public static class PrototypeValidator
    {
        public static bool Validate(Prototype prototype, out string error)
        {
            if (string.IsNullOrWhiteSpace(prototype.ID))
            {
                error = "Prototype ID is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(prototype.Type))
            {
                error = "Prototype type is required.";
                return false;
            }
            error = "god fucking dammit";
            return true;
        }
    }
}