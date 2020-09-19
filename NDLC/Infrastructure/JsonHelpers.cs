using NDLC.Messages;
using Newtonsoft.Json.Linq;

namespace NDLC.Infrastructure
{
    public static class JsonHelpers
    {
		public static JObject ExportStateJObject(this DLCTransactionBuilder builder)
		{
			return JObject.Parse(builder.ExportState());
		}
    }
}