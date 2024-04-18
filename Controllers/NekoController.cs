using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Relational;
using NekoBackend.Models;
using System.Data;

namespace NekoBackend.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class NekoController : ControllerBase
	{
		private readonly IConfiguration configuration;

		public NekoController(IConfiguration configuration)
		{
			this.configuration = configuration;
		}

		[HttpGet]
		[Route("GetNekos")]
		public JsonResult GetNekos()
		{
			string queryNeko = "select * from nekos";
			string queryPhotos = "select * from neko_photos";
			string querySpecs = "select * from specs";

			DataTable nekos = new DataTable();
			DataTable photos = new DataTable();
			DataTable specs = new DataTable();

			string sqlDataSource = configuration.GetConnectionString("NekoCon")??throw new Exception("ОБОСАННАЯ ПАРАША");
			MySqlDataReader reader;
			using(MySqlConnection connection = new MySqlConnection(sqlDataSource))
			{
				connection.Open();
				// post main info
				using(MySqlCommand command = new MySqlCommand(queryNeko, connection))
				{
					reader = command.ExecuteReader();
					nekos.Load(reader);
					reader.Close();
				}
				// photos
				using (MySqlCommand command = new MySqlCommand(queryPhotos, connection))
				{
					reader = command.ExecuteReader();
					photos.Load(reader);
					reader.Close();
				}
				// specs
				using (MySqlCommand command = new MySqlCommand(querySpecs, connection))
				{
					reader = command.ExecuteReader();
					specs.Load(reader);
					reader.Close();
				}

				connection.Close();
			}

			// photosd parse from table
			Dictionary<string, List<string>> photosDict = new Dictionary<string, List<string>>();
			foreach(DataRow row in photos.Rows)
			{
				string photoId = Convert.ToString(row["id"]) ?? "";
				string photoPhoto = Convert.ToString(row["photo"]) ?? "";
				if (photosDict.ContainsKey(photoId))
					photosDict[photoId].Add(photoPhoto);
				else
					photosDict[photoId] = new List<string>() { photoPhoto };
			}

			// specs parse from table
			Dictionary<string, Dictionary<string, string>> specsDict = new Dictionary<string, Dictionary<string, string>>();
			foreach (DataRow row in specs.Rows)
			{
				string specId = Convert.ToString(row["id"]) ?? "";
				string spec = Convert.ToString(row["spec"]) ?? "";
				string specValue = Convert.ToString(row["spec_value"]) ?? "";
				if (specsDict.ContainsKey(specId))
					specsDict[specId][spec] = specValue;
				else
					specsDict[specId] = new Dictionary<string, string>() { { spec, specValue } };
			}

			List<NekoPost> Posts = new List<NekoPost>();
			foreach (DataRow row in nekos.Rows)
			{
				int id = Convert.ToInt32(row["id"]);
				string sId = Convert.ToString(row["id"]) ?? "";
				string name = Convert.ToString(row["name"]) ?? "";
				string image = Convert.ToString(row["image"]) ?? "";
				decimal price = Convert.ToDecimal(row["price"]);
				string description = Convert.ToString(row["desction"]) ?? "";
				string[] photosForThis = photosDict.Where(p => p.Key == sId).Select(p => p.Value.ToArray()).SingleOrDefault() ?? [];
				Dictionary<string, string> specsForThis = specsDict.Where(s => s.Key == sId).Select(s => s.Value).SingleOrDefault() ?? new Dictionary<string, string>();

				NekoPost neko = new()
				{
					Id = id,
					Name = name,
					Image = image,
					Price = price,
					Description = description,
					Photos = photosForThis,
					Specifications = specsForThis
				};
				Posts.Add(neko);
			}

			return new JsonResult(Posts);
		}
	}
}
