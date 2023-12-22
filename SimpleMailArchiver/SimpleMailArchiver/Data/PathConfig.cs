namespace SimpleMailArchiver.Data
{
    public record class PathConfig
    {
        public string ArchiveBasePath { get; init; } = "";

        public string ImportBasePath { get; init; } = "";

        public string AccountConfigsPath { get; init; } = "";

        public string DbPath { get; init; } = "";
    }
}

