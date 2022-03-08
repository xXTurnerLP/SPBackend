using System;
using System.Collections.Generic;
using System.Threading;
using MySqlConnector;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace SPBackend
{
	class Program
	{
		// Time between each disk save from memory, time is approximate
		const uint DISK_SAVE_TIME_PERIOD_SECONDS = 5;
		const ushort HTTP_PORT = 1234;
		const string MYSQL_CONNECTION_STRING = "connection string";



		static object queue_mutex = new object();
		static Queue<string> queue = new Queue<string>();

		static void Main(string[] args)
		{
			Thread httpThread = new Thread(() => HTTPServerThread.Start(HTTP_PORT, queue, queue_mutex));
			httpThread.Start();

			Dictionary<string, ParkingModel> db = new Dictionary<string, ParkingModel>();
			DateTime latestDbAccessTime = DateTime.Now;

			// Populate in-memory db with the real data
			using (var connection = new MySqlConnection(MYSQL_CONNECTION_STRING))
			{
				connection.Open();

				using (var command = new MySqlCommand("SELECT * FROM parkings;", connection))
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						ParkingModel model = new ParkingModel();

						model.Id = reader["Id"].GetType() != typeof(DBNull) ? (string)reader["Id"] : null;
						model.Name = reader["Name"].GetType() != typeof(DBNull) ? (string)reader["Name"] : null;
						model.Address = reader["Address"].GetType() != typeof(DBNull) ? (string)reader["Address"] : null;
						model.GPS_Lat = reader["GPS Lat"].GetType() != typeof(DBNull) ? (double)reader["GPS Lat"] : null;
						model.GPS_Lng = reader["GPS Lng"].GetType() != typeof(DBNull) ? (double)reader["GPS Lng"] : null;
						model.Total = reader["Total"].GetType() != typeof(DBNull) ? (int)reader["Total"] : null;
						model.Free = reader["Free"].GetType() != typeof(DBNull) ? (int)reader["Free"] : null;
						model.Type = reader["Type"].GetType() != typeof(DBNull) ? (string)reader["Type"] : null;

						db.Add(model.Id, model);
					}
				}

				Console.WriteLine($"Loaded {db.Count} rows from database");
			}

			while (true)
			{
				lock (queue_mutex)
				{
					if (queue.Count > 0)
					{
						DeviceCfgModel cfg = JsonConvert.DeserializeObject<DeviceCfgModel>(queue.Dequeue().Replace("\\\"", "\""));
						Console.WriteLine($"Got \"{cfg.data}\" from {cfg.id}");
						if (db.ContainsKey(cfg.id))
						{
							ParkingModel model = new ParkingModel();
							model.CopyFrom(cfg);

							if (cfg.data == "ENTER")
							{
								model.Free = db[cfg.id].Free - 1; // negative values are possible
							}
							else if (cfg.data == "LEAVE")
							{
								model.Free = db[cfg.id].Free < cfg.total ? db[cfg.id].Free + 1 : cfg.total;
							}

							db[cfg.id] = model;
						}
						else
						{
							ParkingModel model = new ParkingModel();
							model.CopyFrom(cfg);

							model.Free = cfg.total;

							db.Add(model.Id, model);
						}

						// Save changes to disk (DB)
						if ((DateTime.Now - latestDbAccessTime).TotalSeconds > DISK_SAVE_TIME_PERIOD_SECONDS)
						{
							latestDbAccessTime = DateTime.Now;

							ManualResetEvent waitForCopy = new ManualResetEvent(false);
							Task.Run(() => WriteToDB(db, waitForCopy));
							waitForCopy.WaitOne(); // Waits for the memory db to be copied to another thread
						}
					}
				}

				Thread.Sleep(1);
			}
		}

		static async void WriteToDB(Dictionary<string, ParkingModel> memory_db, ManualResetEvent waitForCopy)
		{
			Dictionary<string, ParkingModel> db = CloneDbDictDeepCopy(memory_db);
			waitForCopy.Set();

			using (var connection = new MySqlConnection(MYSQL_CONNECTION_STRING))
			{
				connection.Open();

				foreach (var e in db)
				{
					using (var command = new MySqlCommand("INSERT INTO parkings (Id, Name, Address, `GPS Lat`, `GPS Lng`, Total, Free, Type) VALUES (@Id, @Name, @Address, @GPS_Lat, @GPS_Lng, @Total, @Free, @Type) AS val ON DUPLICATE KEY UPDATE Name = val.Name, Address = val.Address, `GPS Lat` = val.`GPS Lat`, `GPS Lng` = val.`GPS Lng`, Total = val.Total, Free = val.Free, Type = val.Type;", connection))
					{
						command.Parameters.AddWithValue("Id", e.Key);
						command.Parameters.AddWithValue("Name", e.Value.Name);
						command.Parameters.AddWithValue("Address", e.Value.Address);
						command.Parameters.AddWithValue("GPS_Lat", e.Value.GPS_Lat);
						command.Parameters.AddWithValue("GPS_Lng", e.Value.GPS_Lng);
						command.Parameters.AddWithValue("Total", e.Value.Total);
						command.Parameters.AddWithValue("Free", e.Value.Free);
						command.Parameters.AddWithValue("Type", e.Value.Type);

						await command.ExecuteNonQueryAsync();
					}
				}
			}
		}

		static Dictionary<string, ParkingModel> CloneDbDictDeepCopy(Dictionary<string, ParkingModel> original)
		{
			Dictionary<string, ParkingModel> ret = new Dictionary<string, ParkingModel>(original.Count, original.Comparer);
			foreach (var e in original)
			{
				ParkingModel model = new ParkingModel()
				{
					Id = e.Value.Id,
					Total = e.Value.Total,
					Address = e.Value.Address,
					Name = e.Value.Name,
					Free = e.Value.Free,
					Type = e.Value.Type,
					GPS_Lng = e.Value.GPS_Lng,
					GPS_Lat = e.Value.GPS_Lat
				};

				ret.Add(e.Key, model);
			}
			return ret;
		}
	}
}
