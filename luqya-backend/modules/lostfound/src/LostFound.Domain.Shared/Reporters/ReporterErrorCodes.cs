namespace LostFound.Reporters
{
    public static class ReporterErrorCodes
    {
        private const string Prefix = "LostFound:Reporter:";

        public const string PhoneIsRequiredForGuests = Prefix + "0001";
        public const string ReporterNotFound = Prefix + "0002";
    }
}
