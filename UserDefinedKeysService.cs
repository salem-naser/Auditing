namespace Auditing
{
    public class UserDefinedKeysService
    {
        public HashSet<string> UserDefinedKeys { get; }

        public UserDefinedKeysService(IEnumerable<string> initialKeys)
        {
            UserDefinedKeys = new HashSet<string>(initialKeys);
        }
    }
}
