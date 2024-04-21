using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NekoBackend.Models;
using System.Data;

namespace NekoBackend.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public class NekoController(IConfiguration _configuration) : ControllerBase
	{
		private readonly IConfiguration configuration = _configuration;

		[HttpGet]
		[Route("GetNekos")]
		public JsonResult GetNekos()
		{
			string queryNeko = "select * from nekos";
			string queryPhotos = "select * from neko_photos";
			string querySpecs = "select * from specs";
			string queryBlobs = "select * from neko_blobs";

			DataTable nekos = new();
			DataTable photos = new();
			DataTable specs = new();
			DataTable blobs = new();

			string sqlDataSource = configuration.GetConnectionString("NekoCon") ?? throw new Exception("ЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭ");
			MySqlDataReader reader;
			using(MySqlConnection connection = new(sqlDataSource))
			{
				connection.Open();
				using (MySqlTransaction transaction = connection.BeginTransaction())
				{
					// post main info
					using (MySqlCommand command = new(queryNeko, connection))
					{
						reader = command.ExecuteReader();
						nekos.Load(reader);
						reader.Close();
					}
					// photos
					using (MySqlCommand command = new(queryPhotos, connection))
					{
						reader = command.ExecuteReader();
						photos.Load(reader);
						reader.Close();
					}
					// specs
					using (MySqlCommand command = new(querySpecs, connection))
					{
						reader = command.ExecuteReader();
						specs.Load(reader);
						reader.Close();
					}
					// blobs
					using (MySqlCommand command = new(queryBlobs, connection))
					{
						reader = command.ExecuteReader();
						blobs.Load(reader);
						reader.Close();
					}

					transaction.Commit();
				}
				connection.Close();
			}

			// photos parse from table
			Dictionary<string, List<string>> photosDict = [];
			foreach(DataRow row in photos.Rows)
			{
				string photoId = Convert.ToString(row["id"]) ?? "";
				string photoPhoto = Convert.ToString(row["photo"]) ?? "";
				if (photosDict.ContainsKey(photoId))
					photosDict[photoId].Add(photoPhoto);
				else
					photosDict[photoId] = [photoPhoto];
			}

			// parse blobs to strings and add them to photosDict
			Dictionary<string, byte[]> blobsDict = [];
			foreach (DataRow row in blobs.Rows)
			{
				string blobId = Convert.ToString(row["id"]) ?? "";

				byte[] blobImage = row["image"] as byte[] ?? [];
				var base64Image = Convert.ToBase64String(blobImage);
				string blobStr = $"data:image/octet-stream;base64,{base64Image}";

				if (photosDict.ContainsKey(blobId))
					photosDict[blobId].Add(blobStr);
				else
					photosDict[blobId] = [blobStr];
			}

			// specs parse from table
			Dictionary<string, Dictionary<string, string>> specsDict = [];
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

			List<NekoPost> Posts = [];
			foreach (DataRow row in nekos.Rows)
			{
				int id = Convert.ToInt32(row["id"]);
				string sId = Convert.ToString(row["id"]) ?? "";
				string name = Convert.ToString(row["name"]) ?? "";
				string image = Convert.ToString(row["image"]) ?? "";
				decimal price = Convert.ToDecimal(row["price"]);
				string description = Convert.ToString(row["desction"]) ?? "";
				string[] photosForThis = photosDict.Where(p => p.Key == sId).Select(p => p.Value.ToArray()).SingleOrDefault() ?? [];
				Dictionary<string, string> specsForThis = specsDict.Where(s => s.Key == sId).Select(s => s.Value).SingleOrDefault() ?? [];

				if (image == "любое")
					image = photosForThis.ElementAtOrDefault(0) ?? "";

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

		[HttpPatch]
		[Route("EditNeko")]
		public JsonResult EditNeko(NekoPost neko)
		{
			try
			{
				int id = neko.Id;
				string queryNeko = "update nekos set " +
					"name = @name, " +
					"image = @image, " +
					"price = @price, " +
					"desction = @desc " +
					$"where id = {id}";

				string querySpecsDelete = $"delete from specs where id = {id}";
				string querySpecsInsert = "insert into specs values" + string.Join(", ", neko.Specifications.Select(s => $"({id}, \"{s.Key}\", \"{s.Value}\")"));

				string[] photos = neko.Photos.Where(p => p.StartsWith("http")).ToArray();
				string queryPhotosDelete = $"delete from neko_photos where id = {id}";
				string queryPhotosInsert = "insert into neko_photos values" + string.Join(", ", photos.Select(p => $"({id}, \"{p}\")"));

				string[] blobsStrings = neko.Photos.Where(p => !p.StartsWith("http")).Select(p => string.Join("", p.Split(',').Skip(1))).ToArray();
				List<byte[]> blobs = blobsStrings.Select(Convert.FromBase64String).ToList();
				string queryBlobsDelete = $"delete from neko_blobs where id = {id}";
				string queryBlobsInsert = $"insert into neko_blobs values" + string.Join(", ", Enumerable.Range(0, blobs.Count).Select(e => $"({id}, @blob{e})"));

				string sqlDataSource = configuration.GetConnectionString("NekoCon") ?? throw new Exception("ЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭ");
				using (MySqlConnection connection = new(sqlDataSource))
				{
					connection.Open();
					using(MySqlTransaction transaction = connection.BeginTransaction())
					{
                        // simple nekos table upd
                        using (MySqlCommand command = new(queryNeko, connection)) 
						{
							command.Parameters.AddWithValue("@name", neko.Name);
							command.Parameters.AddWithValue("@price", neko.Price);
							command.Parameters.AddWithValue("@image", neko.Image);
							command.Parameters.AddWithValue("@desc", neko.Description);
							command.ExecuteNonQuery();
						}
                        // specs
                        if (neko.Specifications.Count > 0)
						{
							using (MySqlCommand command = new(querySpecsDelete, connection)) { command.ExecuteNonQuery(); }
							using (MySqlCommand command = new(querySpecsInsert, connection)) { command.ExecuteNonQuery(); }
						}
                        // photos
                        if (photos.Length > 0)
						{
							using (MySqlCommand command = new(queryPhotosDelete, connection)) { command.ExecuteNonQuery(); }
							using (MySqlCommand command = new(queryPhotosInsert, connection)) { command.ExecuteNonQuery(); }
						}
                        //blobs
                        if (blobs.Count > 0)
						{
							using (MySqlCommand command = new(queryBlobsDelete, connection)) { command.ExecuteNonQuery(); }
							using (MySqlCommand command = new(queryBlobsInsert, connection)) 
							{
								for (int i = 0; i < blobs.Count; i++)
									command.Parameters.AddWithValue($"@blob{i}", blobs[i]);
								command.ExecuteNonQuery();
							}
						}
						transaction.Commit();
					}
					connection.Close();
				}
                Console.WriteLine("УРААААААААААААААА");
                return new JsonResult("УДАЧНО ОБНОВЛЕННО");
			}
			catch (Exception e)
			{
                Console.WriteLine($"ОШИБКА: {e}");
				return new JsonResult(new { IsSuccessStatusCode = false, Message =  e.Message });
			}
		}

		[HttpPost]
		[Route("AddNeko")]
		public JsonResult AddNeko(NekoPost neko)
		{
			try
			{
				string queryNeko = "insert into nekos (name, image, price, desction) values(@name, @image, @price, @desc)";
				string queryAll = "select id from nekos";

				string sqlDataSource = configuration.GetConnectionString("NekoCon") ?? throw new Exception("ЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭЭ");
				MySqlDataReader reader;
				DataTable idsTable = new();

				using (MySqlConnection connection = new(sqlDataSource))
				{
					connection.Open();
					// simple nekos table add
					using (MySqlCommand command = new(queryNeko, connection))
					{
						Console.WriteLine(neko.Price);
						command.Parameters.AddWithValue("@name", neko.Name);
						command.Parameters.AddWithValue("@image", neko.Image);
						command.Parameters.AddWithValue("@price", neko.Price);
						command.Parameters.AddWithValue("@desc", neko.Description);
						command.ExecuteNonQuery();
					}
					using (MySqlCommand command = new(queryAll, connection)) 
					{ 
						reader = command.ExecuteReader();
						idsTable.Load(reader);
						reader.Close();
					}
					connection.Close();
				}
				int id = 0;
				foreach(DataRow row in idsTable.Rows)
					id = Convert.ToInt32(row["id"]);

				string querySpecsInsert = "insert into specs values" + string.Join(", ", neko.Specifications.Select(s => $"({id}, \"{s.Key}\", \"{s.Value}\")"));

				string[] photos = neko.Photos.Where(p => p.StartsWith("http")).ToArray();
				string queryPhotosInsert = "insert into neko_photos values" + string.Join(", ", photos.Select(p => $"({id}, \"{p}\")"));

				string[] blobsStrings = neko.Photos.Where(p => !p.StartsWith("http")).Select(p => string.Join("", p.Split(',').Skip(1))).ToArray();
				List<byte[]> blobs = blobsStrings.Select(Convert.FromBase64String).ToList();
				string queryBlobsInsert = $"insert into neko_blobs values" + string.Join(", ", Enumerable.Range(0, blobs.Count).Select(e => $"({id}, @blob{e})"));

				using (MySqlConnection connection = new(sqlDataSource))
				{
					connection.Open();
					using (MySqlTransaction transaction = connection.BeginTransaction())
					{
						// specs
						if (neko.Specifications.Count > 0) using (MySqlCommand command = new(querySpecsInsert, connection)) { command.ExecuteNonQuery(); }
						// photos
						if (photos.Length > 0) using (MySqlCommand command = new(queryPhotosInsert, connection)) { command.ExecuteNonQuery(); }
						//blobs
						if (blobs.Count > 0)
							using (MySqlCommand command = new(queryBlobsInsert, connection))
							{
								for (int i = 0; i < blobs.Count; i++)
									command.Parameters.AddWithValue($"@blob{i}", blobs[i]);
								command.ExecuteNonQuery();
							}
						transaction.Commit();
					}
					connection.Close();
				}
				Console.WriteLine("УРААААААААААААААА");
				return new JsonResult("УДАЧНО ОБНОВЛЕННО");
			}
			catch (Exception e)
			{
				Console.WriteLine($"ОШИБКА: {e}");
				return new JsonResult(e.Message);
			}
		}
	}
}
