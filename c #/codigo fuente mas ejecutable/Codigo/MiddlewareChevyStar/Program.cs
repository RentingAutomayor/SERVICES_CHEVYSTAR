using MiddlewareChevyStar.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace MiddlewareChevyStar
{
    class Program
    {
        static void Main(string[] args)
        {
            //Recuperar ruta de directorio donde se guardaran los logs para crearla si no existe
            string path = ConfigurationManager.AppSettings["DirectorioLogs"];
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            //Información para los Logs
            //Sea A = El conjunto de activos de Maximo
            //Sea B = El Conjunto de Activos de ChevyStar

            //Indicador de proceso
            Console.WriteLine("Procesando información de ChevyStar...");

            //Fecha actual para los logs
            var fecha = DateTime.Now.ToString("yyyy-MM-dd");

            //Línea de código para tratar la conversión de string a decimal en el formato que lo envía chevystar
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            //Invocamos el servicio de ChevyStar
            ChevyStarService.OnlineSoapClient CSClient = new ChevyStarService.OnlineSoapClient();

            //Información de logeo
            ChevyStarService.LoginInfo Credentials = new ChevyStarService.LoginInfo();
            Credentials.Company = ConfigurationManager.AppSettings["company"];
            Credentials.Username = ConfigurationManager.AppSettings["user"];
            Credentials.Password = ConfigurationManager.AppSettings["pass"];

            string content = string.Empty;

            try
            {
                //Obtener los activos sincronizados con chevystar y con con medidor, por medio de una petición http con protocolo TLS 1.2
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    ConfigurationManager.AppSettings["UrlRESTMaximo"] +
                    ConfigurationManager.AppSettings["RAEO_ACTIVOSCHEV"] +
                    ConfigurationManager.AppSettings["credencialesMaximo"] +
                    ConfigurationManager.AppSettings["Query1"]);
                request.Method = "GET";
                //request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 58.0.3029.110 Safari / 537.36";
                request.UserAgent = "Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 71.0.3578.98 Safari / 537.36";
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                //Esta línea es para poner de acuerdo esta aplicación con el RESTService de maximo en cuanto al protocolo de comunicación seguro dado que es https
                ServicePointManager.Expect100Continue = true;
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                HttpResponseMessage response = new HttpResponseMessage();

                using (HttpClient client = new HttpClient())
                {
                    var url = ConfigurationManager.AppSettings["UrlRESTMaximo1"] +
                    ConfigurationManager.AppSettings["RAEO_ACTIVOSCHEV"] +
                    ConfigurationManager.AppSettings["credencialesMaximo"] +
                    ConfigurationManager.AppSettings["Query1"];

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

                    response = client.GetAsync(url).Result;
                }


                using (Stream stream = response.Content.ReadAsStreamAsync().Result)
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        content = sr.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            //Espacio de nombres para leer los nodos XML
            XNamespace headNameSpace = ConfigurationManager.AppSettings["headNameSpace"];
            //Mapear la respuesta en los objetos modelo que guardarán la información necesaria
            XDocument doc = XDocument.Parse(content);

            var documento = doc.Descendants(headNameSpace + "ASSET");

            //Obtener listado de activos
            try
            {
                List<ASSET> ASSETS = new List<ASSET>();
                /* List<ASSET> ASSETS = (from ASSET in documento
                                       select new ASSET()
                                       {
                                           ASSETNUM = ASSET.Descendants(headNameSpace + "ASSETNUM").FirstOrDefault().Value,
                                           RAULTIMAMEDICION = Convert.ToDouble(ASSET.Descendants(headNameSpace + "ASSETMETER").FirstOrDefault().Descendants(headNameSpace + "LASTREADING").FirstOrDefault().Value)
                                       }).ToList();
                                       */
                foreach (var item in documento)
                {
                    try
                    {
                        var nodokm = item.Descendants(headNameSpace + "ASSETMETER");
                        if (nodokm != null)
                        {
                            ASSET placa = new ASSET();
                            placa.ASSETNUM = item.Descendants(headNameSpace + "ASSETNUM").FirstOrDefault().Value;
                            placa.RAULTIMAMEDICION = 0; //Convert.ToDouble(item.Descendants(headNameSpace + "ASSETMETER").FirstOrDefault().Descendants(headNameSpace + "LASTREADING").FirstOrDefault().Value);
                            Console.WriteLine(placa.ASSETNUM);
                            Console.WriteLine(Convert.ToDouble(item.Descendants(headNameSpace + "ASSETMETER").FirstOrDefault().Descendants(headNameSpace + "LASTREADING").FirstOrDefault().Value));
                            ASSETS.Add(placa);
                        }
                        Console.WriteLine("Proceso Ejecutado Satisfactoriamente");
                    }

                    catch (Exception e) {
                        Console.WriteLine("No se encontro odometro para esta placa");
                        continue;
                    }

                    
                }
                #region log con archivos de los activos que se traen de Maximo (Conjunto A)
                // crear el path
                if (ConfigurationManager.AppSettings["LogActivosMaximo"] == "ACTIVO")
                {
                    var archivo = ConfigurationManager.AppSettings["DirectorioLogs"] + "LogActivosMaximo_" + fecha + ".txt";

                    // eliminar el fichero si ya existe
                    if (File.Exists(archivo))
                        File.Delete(archivo);

                    // crear el fichero y guardar los activos de la consulta de maximo
                    using (var fileStream = File.Create(archivo))
                    {
                        var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO          ÚLTIMA MEDICIÓN \n");
                        fileStream.Write(Cabecera, 0, Cabecera.Length);
                        foreach (var item in ASSETS)
                        {
                            var registro = new UTF8Encoding(true).GetBytes(item.ASSETNUM + "          " + item.RAULTIMAMEDICION + " \n");
                            fileStream.Write(registro, 0, registro.Length);

                        }
                        fileStream.Flush();
                        fileStream.Close();
                    }

                }

                #endregion

                //Obtener información de todos los vehículos de ChevyStar y guardarlo en una lista del modelo a enviar a maximo
                var todosLosVehiculos = CSClient.GetCarsInfo(Credentials).ToList();

                #region log con todos activos de ChevyStar (Conjunto B)
                // crear el path
                if (ConfigurationManager.AppSettings["LogActivosChevystar"] == "ACTIVO")
                {
                    var archivoChevystar = ConfigurationManager.AppSettings["DirectorioLogs"] + "LogActivosChevystar_" + fecha + ".txt";

                    // eliminar el fichero si ya existe
                    if (File.Exists(archivoChevystar))
                        File.Delete(archivoChevystar);

                    // crear el fichero y guardar los activos de la consulta de ChevyStar
                    using (var fileStream = File.Create(archivoChevystar))
                    {
                        var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO          ÚLTIMA MEDICIÓN \n");
                        fileStream.Write(Cabecera, 0, Cabecera.Length);
                        foreach (var item in todosLosVehiculos)
                        {
                            //Separar en un array la cadena concatenada por #
                            var ArrayVehicle_Tool_Tip = item.Vehicle_Tool_Tip.Split('#');

                            //Obtener el elemento del array que tenga Kilometros

                            double Kilometros = Convert.ToDouble(Array.Find(ArrayVehicle_Tool_Tip, (x => x.Contains(ConfigurationManager.AppSettings["CampoCriterio1"]))).Split('=')[1]);
                            var registro = new UTF8Encoding(true).GetBytes(item.Vehicle_Label + "          " + Kilometros + " \n");
                            fileStream.Write(registro, 0, registro.Length);

                        }
                        //Escribir los bytes
                        fileStream.Flush();
                        //Cerrar la transacción una vez completada
                        fileStream.Close();
                    }

                }

                #endregion

                //Lista de mediciones(Kilómetros) a enviar a maximo
                List<METERDATA> MedicionesAEnviar = new List<METERDATA>();

                //Listado de activos de maximo 
                List<string> TodosActivosMaximo = ASSETS.Select(x => x.ASSETNUM).ToList<string>();
                List<string> TodosActivosChevystar = todosLosVehiculos.Select(x => x.Vehicle_Label).ToList<string>();

                //Activos filtrados de ambos conjuntos A-B  y B-A
                List<string> ActivosChevystarNoEncontrados = TodosActivosChevystar.Except(TodosActivosMaximo).ToList<string>();
                List<string> ActivosMaximoNoEncontrados = TodosActivosMaximo.Except(TodosActivosChevystar).ToList<string>();

                var archivoMaximoNo = ConfigurationManager.AppSettings["DirectorioLogs"] + "LogActivosMaximoNoEncontrados_" + fecha + ".txt";
                var archivoChevystarNo = ConfigurationManager.AppSettings["DirectorioLogs"] + "LogActivosChevystarNoEncontrados_" + fecha + ".txt";

                List<ASSET> MedicionesErroneas = new List<ASSET>();
                List<ASSET> ActivosMasDeDoceCaracteres = new List<ASSET>();

                //Detectar que activos de chevystar poseen mas de doce caracteres
                foreach (var item in TodosActivosChevystar)
                {
                    if (item.Length > 12)
                    {
                        ActivosMasDeDoceCaracteres.Add(new ASSET() { ASSETNUM = item });
                    }
                }

                //Validación de longitud de caracteres del activos que viene de chevystar Se logea la placa que se amayor a 12 caracteres (Longitud de ASSETNUM en maximo)


                //Armar la lista de mediciones a enviar a maximo Por activo Sincronizado
                foreach (var item in ASSETS)
                {
                    bool activoEncontrado = todosLosVehiculos.Exists(x => x.Vehicle_Label == item.ASSETNUM);

                    //Filtrar los datos de chevystar de modo que solo se envíen las mediciones de activos que existen en maximo (Hacer Match)
                    var vehiculo = todosLosVehiculos.FirstOrDefault(x => x.Vehicle_Label == item.ASSETNUM);

                    if (vehiculo != null)
                    {
                        //Separar en un array la cadena concatenada por #
                        var ArrayVehicle_Tool_Tip = vehiculo.Vehicle_Tool_Tip.Split('#');

                        //Obtener el elemento del array que tenga Kilometros
                        double Kilometros = Convert.ToDouble(Array.Find(ArrayVehicle_Tool_Tip, (x => x.Contains(ConfigurationManager.AppSettings["CampoCriterio1"]))).Split('=')[1]);

                        //Si la medición viene de chevystar con valores negativos o fuera del rango 0 a 999999 entonces se guarda en la lista de activos con errores y no se envía medición a Maximo
                        if (Kilometros < 0 && Kilometros >= 999999)
                        {
                            MedicionesErroneas.Add(new ASSET() { ASSETNUM = vehiculo.Vehicle_Label, RAULTIMAMEDICION = Kilometros });
                        }
                        else
                        {
                            //Verificar si la medición obtenida de chevystar es mayor a la ultima registrada para realizar la petición, de lo contrario la petición no de efectua.
                            if (item.RAULTIMAMEDICION < Kilometros)
                            {
                                MedicionesAEnviar.Add(new METERDATA()
                                {
                                    ASSETNUM = item.ASSETNUM,
                                    METERNAME = ConfigurationManager.AppSettings["MEDIDOR"],
                                    NEWREADING = Kilometros,
                                    SITEID = ConfigurationManager.AppSettings["SITEID"]
                                });
                            }
                        }

                    }

                }

                #region Logear activos de chevystar con errores en la medición

                var archivoActivosErroneos = ConfigurationManager.AppSettings["DirectorioLogs"] + "ActivosErroneos" + fecha + ".txt";

                // eliminar el fichero si ya existe
                if (File.Exists(archivoActivosErroneos))
                    File.Delete(archivoActivosErroneos);

                // crear el fichero y guardar los activos con mediciones erroneas
                using (var fileStream = File.Create(archivoActivosErroneos))
                {
                    var Texto = new UTF8Encoding(true).GetBytes("Los siguientes activos presentaron mediciones por fuera del rango 0 a 999999 \n");
                    fileStream.Write(Texto, 0, Texto.Length);
                    var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO      MEDICIÓN \n");
                    fileStream.Write(Cabecera, 0, Cabecera.Length);
                    foreach (var medicionError in MedicionesErroneas)
                    {
                        var registro = new UTF8Encoding(true).GetBytes(medicionError.ASSETNUM + "       " + medicionError.RAULTIMAMEDICION + " \n");
                        fileStream.Write(registro, 0, registro.Length);
                    }
                    //Escribir los bytes
                    fileStream.Flush();
                    //Cerrar la transacción una vez completada
                    fileStream.Close();
                }

                #endregion

                #region Log de activos de chevystar con mas de doce caracteres

                var ActivosPlacaErronea = ConfigurationManager.AppSettings["DirectorioLogs"] + "ActivosPlacaErronea" + fecha + ".txt";

                // eliminar el fichero si ya existe
                if (File.Exists(ActivosPlacaErronea))
                    File.Delete(ActivosPlacaErronea);

                // crear el fichero y guardar los activos de placa erronea
                using (var fileStream = File.Create(ActivosPlacaErronea))
                {
                    var Texto = new UTF8Encoding(true).GetBytes("Los siguientes activos presentaron placas por encima de doce caracteres (No se ingresaron a Maximo estas mediciones) \n");
                    fileStream.Write(Texto, 0, Texto.Length);
                    var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO \n");
                    fileStream.Write(Cabecera, 0, Cabecera.Length);
                    foreach (var ActivoDoce in ActivosMasDeDoceCaracteres)
                    {
                        var registro = new UTF8Encoding(true).GetBytes(ActivoDoce.ASSETNUM + " \n");
                        fileStream.Write(registro, 0, registro.Length);
                    }
                    //Escribir los bytes
                    fileStream.Flush();
                    //Cerrar la transacción una vez completada
                    fileStream.Close();
                }

                #endregion

                #region Log de activos de Chevistar que no hicieron match (Conjunto B-A)
                if (ConfigurationManager.AppSettings["LogActivosChevystarNoEncontrados"] == "ACTIVO")
                {
                    // eliminar el fichero si ya existe
                    if (File.Exists(archivoChevystarNo))
                        File.Delete(archivoChevystarNo);

                    // crear el fichero y guardar los activos de la consulta de ChevyStar
                    using (var fileStream = File.Create(archivoChevystarNo))
                    {
                        var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO \n");
                        fileStream.Write(Cabecera, 0, Cabecera.Length);
                        foreach (var itemChevNo in ActivosChevystarNoEncontrados)
                        {
                            var registro = new UTF8Encoding(true).GetBytes(itemChevNo + " \n");
                            fileStream.Write(registro, 0, registro.Length);
                        }
                        //Escribir los bytes
                        fileStream.Flush();
                        //Cerrar la transacción una vez completada
                        fileStream.Close();
                    }
                }

                #endregion

                #region Log de activos de maximo que no hicieron match (Conjunto A-B)

                if (ConfigurationManager.AppSettings["LogActivosMaximoNoEncontrados"] == "ACTIVO")
                {
                    // eliminar el fichero si ya existe
                    if (File.Exists(archivoMaximoNo))
                        File.Delete(archivoMaximoNo);

                    // crear el fichero y guardar los activos de la consulta de ChevyStar
                    using (var fileStream = File.Create(archivoMaximoNo))
                    {
                        var Cabecera = new UTF8Encoding(true).GetBytes("ACTIVO \n");
                        fileStream.Write(Cabecera, 0, Cabecera.Length);
                        foreach (var itemMaximoNo in ActivosMaximoNoEncontrados)
                        {
                            var registro = new UTF8Encoding(true).GetBytes(itemMaximoNo + " \n");
                            fileStream.Write(registro, 0, registro.Length);
                        }
                        //Escribir los bytes
                        fileStream.Flush();
                        //Cerrar la transacción una vez completada
                        fileStream.Close();
                    }
                }

                #endregion

                #region Log CSV de archivo con las mediciones que se fueron a maximo (Conjunto A ∩ B)

                if (ConfigurationManager.AppSettings["LogActivosMaximoNoEncontrados"] == "ACTIVO")
                {
                    //Ruta de acceso para el archivo de contingencia
                    var archivoMeterData = ConfigurationManager.AppSettings["DirectorioLogs"] + "LogMedicionesAMaximo_" + fecha + ".csv";

                    // eliminar el fichero si ya existe
                    if (File.Exists(archivoMeterData))
                        File.Delete(archivoMeterData);

                    //Generar un csv de contingnecia con la información a enviar a maximo
                    using (var fileStream = File.Create(archivoMeterData))
                    {
                        var Cabecera = new UTF8Encoding(true).GetBytes("ASSETNUM;METERNAME;NEWREADING;SITEID \n");
                        fileStream.Write(Cabecera, 0, Cabecera.Length);
                        foreach (var item in MedicionesAEnviar)
                        {
                            var registro = new UTF8Encoding(true).GetBytes(item.ASSETNUM + ";" + item.METERNAME + ";" + item.NEWREADING + ";" + item.SITEID + " \n");
                            fileStream.Write(registro, 0, registro.Length);

                        }
                        fileStream.Flush();
                        fileStream.Close();
                    }
                }

                #endregion

                foreach (var item in MedicionesAEnviar)
                {
                    try
                    {
                        //realizar petición POST a MAXIMO para guardar los kilometrajes en el activo respectivo
                        HttpWebRequest requestMaximo = (HttpWebRequest)WebRequest.Create(
                            ConfigurationManager.AppSettings["UrlRESTMaximo"] +
                            ConfigurationManager.AppSettings["MXMETERDATA"] +
                            ConfigurationManager.AppSettings["credencialesMaximo"] +
                            "&ASSETNUM=" + item.ASSETNUM + "&METERNAME=" +
                            ConfigurationManager.AppSettings["MEDIDOR"] +
                            "&NEWREADING=" + item.NEWREADING + "&SITEID=" + "RTAM");
                        //ConfigurationManager.AppSettings["SITEID"]);

                        requestMaximo.Method = "POST";
                        requestMaximo.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 58.0.3029.110 Safari / 537.36";
                        requestMaximo.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                        System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        HttpWebResponse responseMaximo = (HttpWebResponse)requestMaximo.GetResponse();
                    }
                    catch (Exception e)
                    {
                        using (var fileStream = File.Create(archivoActivosErroneos))
                        {
                            var Texto = new UTF8Encoding(true).GetBytes(e.Message);
                            fileStream.Write(Texto, 0, Texto.Length);
                            //Escribir los bytes
                            fileStream.Flush();
                            //Cerrar la transacción una vez completada
                            fileStream.Close();
                        }
                    }
                }
                
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }

}
