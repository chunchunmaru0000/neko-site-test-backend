namespace NekoBackend.Models
{
	public class NekoPost
	{
		public int Id { get; set; }
		public required string Image { get; set; }
		public decimal Price { get; set; }
		public required string Name { get; set; }
		public required string Description { get; set; }
		public required Dictionary<string, string> Specifications { get; set; }
		public required string[] Photos { get; set; }
	}
}
