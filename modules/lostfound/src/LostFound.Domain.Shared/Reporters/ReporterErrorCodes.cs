namespace LostFound.Reporters
{
    public static class ReporterErrorCodes
    {
        private const string Prefix = "LostFound:Reporter:";

        public const string PhoneIsRequiredForGuests = Prefix + "0001";
        public const string ReporterNotFound = Prefix + "0002";
        public const string ReportOwnerNotClaimed = Prefix + "0003";

        // 0004 is reserved by the frontend (ReportLost.jsx/ReportFound.jsx)
        // for a distinct, not-yet-implemented "phone already registered to
        // another account" check - do not reuse it here.
        public const string ClaimTokenInvalid = Prefix + "0005";
        public const string ReporterAlreadyLinked = Prefix + "0006";
    }
}
