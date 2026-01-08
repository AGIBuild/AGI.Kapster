using Avalonia.Input;
using System.Threading.Tasks;

namespace AGI.Kapster.Tests
{
    public class TestClipboard
    {
        public void Test()
        {
            var dataObject = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DataFormat.CreateStringPlatformFormat("format"), "data");
            dataObject.Add(item);
        }
    }
}
