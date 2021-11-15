using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using JuegoMultiplicaciones.Models;
using Newtonsoft.Json;

namespace JuegoMultiplicaciones.Viewmodel
{
    public class ServidorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        System.Timers.Timer t = new System.Timers.Timer();

        HttpListener server = new();
        public List<string> Jugadores { get; set; } = new();
        public bool? RecibirRespuestas { get; set; }
        public ObservableCollection<Jugador> RespuestasUsuarios { get; set; } = new();
        public byte Num1 { get; set; }
        public byte Num2 { get; set; }
        public int RespuestaCorrecta { get; set; }
        public Stopwatch Cronometro { get; set; }
        public IEnumerable<Jugador> TablaResultados =>
            RespuestasUsuarios.OrderBy(x => x.Correcto).ThenBy(x => x.Tiempo);
        public int SegundosRestantes => 30 - Cronometro.Elapsed.Milliseconds;
        public ICommand IniciarCommand { get; set; }
        Dispatcher dispatcher;
        public ServidorViewModel()
        {
            IniciarCommand = new RelayCommand(EjecutarRonda);

            dispatcher = Dispatcher.CurrentDispatcher;
            new Thread(Start).Start();
            t.Elapsed += T_Elapsed;
        }

        private void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Cronometro.Elapsed.Seconds >= 30)
            {
                RecibirRespuestas = false;
                t.Stop();

                Actualizar(nameof(RecibirRespuestas));
                Actualizar(nameof(TablaResultados));
            }

            Actualizar(nameof(SegundosRestantes));
        }

        public void Start()
        {
            if (!server.IsListening)
            {
                server.Prefixes.Add("http://*:80/");
                server.Start();
                Receive();
            }
        }

        void Receive()
        {
            while (server.IsListening)
            {
                var context = server.GetContext();

                if (context.Request.Url.AbsolutePath == "/Jugador" && context.Request.HttpMethod == "POST")
                {
                    //Agregar nombre de jugador
                    if (context.Request.QueryString["username"] != null)
                    {
                        if (Jugadores.Contains(context.Request.QueryString["username"]))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        }
                        else
                        {
                            Jugadores.Add(context.Request.QueryString["username"]);
                            context.Response.StatusCode = 200;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }

                    context.Response.Close();

                }
                else if (context.Request.Url.AbsolutePath == "/Intentar" && context.Request.HttpMethod == "POST")
                {
                    if (RecibirRespuestas == true)
                    {

                        if (context.Request.QueryString["valor"] != null)
                        {
                            if (!Jugadores.Contains(context.Request.QueryString["username"]))
                            {
                                dispatcher.Invoke(() =>
                                {
                                    RespuestasUsuarios.Add(new Jugador
                                    {
                                        Respuesta = int.Parse(context.Request.QueryString["valor"]),
                                        Nombre = context.Request.QueryString["username"],
                                        Tiempo = DateTime.Now,
                                        Correcto = int.Parse(context.Request.QueryString["valor"]) == RespuestaCorrecta
                                    });
                                });

                                context.Response.StatusCode = 200;

                            }
                            else
                            {
                                byte[] buffer = Encoding.UTF8.GetBytes("Ya has respondido.");
                                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                                context.Response.StatusCode = 409;
                            }
                        }
                        else
                        {
                            context.Response.ContentType = "application/json";
                            int segundosRestantes = 30 - Cronometro.Elapsed.Seconds;
                            byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(segundosRestantes));
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            context.Response.StatusCode = 400;
                        }

                    }
                    else
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes("El servidor no ha iniciado la ronda.");
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.StatusCode = 409;
                    }

                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }


            }
        }
        private void EjecutarRonda()
        {
            if (RecibirRespuestas == false)
            {
                Random r = new();
                Num1 = (byte)r.Next(0, 100); 
                Num2 = (byte)r.Next(0, 100);
                RespuestaCorrecta = Num1 * Num2;
                RecibirRespuestas = true;
                Cronometro.Reset();
                Cronometro.Start();

                t.Interval = 1000;
                t.Start();

                Actualizar();

            }

        }

         void Actualizar(string propiedad = null)
        {
            dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
            });
        }
    }
}
