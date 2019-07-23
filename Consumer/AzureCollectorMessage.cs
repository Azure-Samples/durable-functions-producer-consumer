namespace Consumer
{
    class AzureCollectorMessage : Models.CollectorMessage
    {
        public override string CloudProvider => @"Azure";
    }
}