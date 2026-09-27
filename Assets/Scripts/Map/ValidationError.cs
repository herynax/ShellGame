namespace ShellGame.Map
{
    public readonly struct ValidationError
    {
        public readonly string Message;
        public ValidationError(string message) => Message = message;
        public override string ToString() => Message;
    }
}