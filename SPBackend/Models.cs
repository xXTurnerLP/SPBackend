using System;
using System.Collections.Generic;
using System.Text;

namespace SPBackend
{
	class ParkingModel
	{
		public string Id;
		public string Name;
		public string Address;
		public double? GPS_Lat;
		public double? GPS_Lng;
		public int? Total;
		public int? Free;
		public string Type;

		// For Debugging
		public override string ToString()
		{
			return $"{Id} {Name} {Address} {GPS_Lat} {GPS_Lng} {Total} {Free} {Type}";
		}

		public ParkingModel CopyFrom(DeviceCfgModel other)
		{
			Id = other.id;
			Name = other.name;
			Address = other.address;
			GPS_Lat = other.GPS_Lat;
			GPS_Lng = other.GPS_Lng;
			Total = other.total;
			Type = other.type;

			return this;
		}
	}

	class DeviceCfgModel
	{
		public string id;
		public string name;
		public string address;
		public double? GPS_Lat;
		public double? GPS_Lng;
		public int? total;
		public string type;
		public string data;
	}
}
