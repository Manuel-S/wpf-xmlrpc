using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using CookComputing.XmlRpc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace WpfXmlRpcTester
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task<byte[]> GetURLContentsAsync(string url, string data, string username, string password)
        {
            // The download will end up in variable content.
            var content = new MemoryStream();

            // Initialize an HttpWebRequest for the current URL.
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.Credentials = new NetworkCredential(username, password);
            webReq.Method = "POST";
            Stream dataStream = webReq.GetRequestStream();
            var text = data.ToCharArray().Select(x => (byte)x).ToArray();
            dataStream.Write(text, 0, text.Length);
            dataStream.Close();

            // Send the request to the Internet resource and wait for
            // the response.
            using (WebResponse response = await webReq.GetResponseAsync())
            {
                // Get the data stream that is associated with the specified url.
                using (Stream responseStream = response.GetResponseStream())
                {
                    await responseStream.CopyToAsync(content);
                }
            }

            // Return the result as a byte array.
            return content.ToArray();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var anfrage = anfragexml.Text;
            CallRemoteMethod(anfrage);
        }

        private async void CallRemoteMethod(string anfrage)
        {
            antwort.Text = "Arbeite...";
            jsonantwort.Text = "Arbeite...";
            try
            {
                var bytes = await GetURLContentsAsync(adresse.Text, anfrage, user.Text, passwort.Password);

                jsonantwort.Text = await JsonString(bytes);

                var str = Encoding.ASCII.GetString(bytes);
                antwort.Text = str;
            }
            catch (Exception exception)
            {
                antwort.Text = "Fehler:\n" + exception.Message;
                jsonantwort.Text = JsonConvert.SerializeObject(exception, Formatting.Indented);
            }
        }

        private async static Task<string> JsonString(byte[] str)
        {
            var c = new XmlRpcSerializer();
            var r = new XmlRpcResponse();
            try
            {
                r = c.DeserializeResponse(new MemoryStream(str), typeof(object));
            }
            catch
            {
            }
            try
            {
                r = c.DeserializeResponse(new MemoryStream(str), typeof(object[]));
            }
            catch
            {
            }
            try
            {
                r = c.DeserializeResponse(new MemoryStream(str), typeof(XmlRpcStruct));
            }
            catch
            {
            }
            try
            {
                r = c.DeserializeResponse(new MemoryStream(str), typeof(XmlRpcStruct[]));
            }
            catch
            {
            }
            
            return await JsonConvert.SerializeObjectAsync(r.retVal, Formatting.Indented);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var ret = "";
            ret += "<?xml version=\"1.0\"?>\n<methodCall>\n<methodName>";
            var str = anfragecode.Text;

            Regex r = new Regex(@"(?<method>[a-zA-Z_][-a-zA-Z_.]*)\((?<params>.*)\)(;|)", RegexOptions.Singleline | RegexOptions.Compiled);

            var match = r.Match(str);

            if (!match.Success)
            {
                jsonantwort.Text = "Syntax Fehler";
                return;
            }

            try
            {
                ret += match.Groups["method"].Value;
                ret += "</methodName>\n<params>\n";
                var parameters = (ICollection)JsonConvert.DeserializeObject("[" + match.Groups["params"].Value + "]");
                var serializer = new XmlRpcSerializer();
                foreach (JToken token in parameters)
                {
                    ret += "<param>\n<value>\n";
                    ret += ProcessParam(token);
                    ret += "</value>\n</param>\n";
                }
                ret += "</params>\n</methodCall>\n";

                anfragexml.Text = ret;
                CallRemoteMethod(ret);
            }
            catch (Exception ex)
            {
                jsonantwort.Text = "Syntax Fehler:\n"+ex.Message;
                return;
            }
        }

        private static string ProcessParam(JToken p)
        {
            string ret = "";
            switch (p.Type)
            {
                case JTokenType.Integer:
                    ret += "<int>" + ((long)p) + "</int>\n";
                    break;
                case JTokenType.String:
                    ret += "<string>" + ((string)p) + "</string>\n";
                    break;
                case JTokenType.Boolean:
                    ret += "<boolean>" + (((bool) p) ? "1" : "0") + "</boolean>";
                    break;
                case JTokenType.Float:
                    ret += "<double>" + ((double)p) + "</double>\n";
                    break;
                case JTokenType.Bytes:
                    var bytes = p.CreateReader().ReadAsBytes();
                    ret += "<base64>" + Convert.ToBase64String(bytes) + "</base64>";
                    break;
                case JTokenType.Date:
                    var date = p.CreateReader().ReadAsDateTime();
                    ret += "<dateTime.iso8601>" + date.Value.ToString("O") + "</dateTime.iso8601>";
                    break;
                case JTokenType.Array:
                    ret += "<array>\n<data>\n";
                    foreach (JToken v in (JArray)p)
                    {
                        ret += "<value>\n";
                        ret += ProcessParam(v);
                        ret += "</value>\n";
                    }
                    ret += "</data>\n</array>\n";
                    break;
                case JTokenType.Object:
                    ret += "<struct>\n";
                    foreach (KeyValuePair<string, JToken> v in (JObject)p)
                    {
                        ret += "<member>\n";
                        if (!string.IsNullOrWhiteSpace(v.Key))
                            ret += "<name>" + v.Key + "</name>\n";
                        ret += "<value>\n";
                        ret += ProcessParam(v.Value);
                        ret += "</value>\n</member>\n";
                    }
                    ret += "</struct>\n";
                    break;
            }
            return ret;
        }

        private void anfragecode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.Key == Key.Enter)
            {
                e.Handled = true;
                Button_Click_2(sender, e);
            }
        }

        private void anfragexml_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.Key == Key.Enter)
            {
                e.Handled = true;
                Button_Click_1(sender, e);
            }
        }
    }
}
