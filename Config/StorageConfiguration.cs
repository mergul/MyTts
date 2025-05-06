namespace MyTts.Storage
{
    public class StorageConfiguration
    {
        public string BasePath { get; set; } = string.Empty;
        public string MetadataPath { get; set; } = string.Empty;
        public CacheDurationConfig CacheDuration { get; set; } = new();
        public GoogleCloudConfig GoogleCloud { get; set; } = new();
        public string DefaultDisk { get; set; } = "local";
        public Dictionary<string, DiskConfig> Disks { get; set; } = new();
    }

    public class CacheDurationConfig
    {
        public TimeSpan Database { get; set; }
        public TimeSpan Files { get; set; }
    }

    public class GoogleCloudConfig
    {
        public string BucketName { get; set; } = string.Empty;
    }

    public class DiskConfig
    {
        public string Driver { get; set; } = string.Empty;
        public string Root { get; set; } = string.Empty;
        public Dictionary<string, string>? Config { get; set; }
    }

    public class FirebaseConfig : DiskConfig
    {
        public new FirebaseSpecificConfig? Config { get; set; }
    }

    public class FirebaseSpecificConfig
    {
        public string BucketName { get; set; } = string.Empty;
        public string AuthJson { get; set; } = string.Empty;
        public string DefaultLanguage { get; set; } = "tr";
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class GoogleCloudDiskConfig : DiskConfig
    {
        public new GoogleCloudSpecificConfig? Config { get; set; }
    }

    public class GoogleCloudSpecificConfig
    {
        public string BucketName { get; set; } = string.Empty;
        public string AuthJson { get; set; } = string.Empty;
    }
}
