namespace HPASharp.Infrastructure
{
	public interface IId
	{
	}

	public struct Id<T> : IId
	{
		private readonly int _value;

		public Id(int value)
		{
			_value = value;
		}

		public static implicit operator int(Id<T> id)
		{
			return id._value;
		}

		public static explicit operator Id<T>(int value)
		{
			return new Id<T>(value);
		}
	}
}
