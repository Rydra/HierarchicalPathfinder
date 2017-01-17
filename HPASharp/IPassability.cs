namespace HPASharp
{
	public interface IPassability
	{
		/// <summary>
		/// Tells whether for a given position this passability class can enter or not.
		/// </summary>
		bool CanEnter(Position pos, out int movementCost);
	}
}
