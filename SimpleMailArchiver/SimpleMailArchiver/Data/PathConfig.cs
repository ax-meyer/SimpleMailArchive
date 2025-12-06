namespace SimpleMailArchiver.Data;

public record class PathConfig
{
    public string ArchiveBasePath { get; init; } = "/etc/mailarchive";

    public string ImportBasePath { get; init; } = "/etc/mailimport";

    public string AccountConfigsPath { get; init; } = "/etc/mailaccounts";

    public string DbPath { get; init; } = "/etc/maildb";
}