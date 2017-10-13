namespace OpenGW
{
    public class Wrapper<T>
    {
        public T Value { get; set; }

        public Wrapper(T value)
        {
            this.Value = value;
        }

        public static explicit operator Wrapper<T>(T value)
        {
            return new Wrapper<T>(value);
        }

        public static implicit operator T(Wrapper<T> wrapper)
        {
            return wrapper.Value;
        }
    }
}