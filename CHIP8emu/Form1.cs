using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Timers;

namespace CHIP8emu
{
    public partial class Form1 : Form
    {
        Graphics maingr;
        CPU chip;
        Thread Drawthread;
        Thread Chipthread;
        int key;
        int speed;
        public Form1()
        {
            InitializeComponent();
        }

        private void Draaaw()
        {
            while(true)
            {
                DrawGrid(maingr, chip.gpu.screen, 12);
                Thread.Sleep(speed * 10);
            }
        }

        private void Chiiiip()
        {
            while (true)
            {
                chip.Tick(key);
                Thread.Sleep(speed);
            }
        }

        public static void DrawGrid(Graphics g, bool[,] data, int sizeofpixel)
        {
            g.Clear(Color.White);
            SolidBrush brush = new SolidBrush(Color.Black);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 32; y++)
                {
                    if (data[x,y])
                        g.FillRectangle(brush, (float)(x * sizeofpixel), (float)(y * sizeofpixel), (float)sizeofpixel, (float)sizeofpixel);
                }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            maingr = CreateGraphics();
            maingr.Clear(Color.White);
            speed = 5;
            //chip.LoadProgram("testmaze.ch8");
            //chip.LoadProgram("picture.ch8");
            //chip.LoadProgram("ibm.ch8");
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            chip = new CPU();
            chip.LoadProgram("pong.ch8");
            Drawthread = new Thread(Draaaw);
            Chipthread = new Thread(Chiiiip);
            Drawthread.Start();
            Chipthread.Start();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Drawthread.Abort();
            Chipthread.Abort();
        }

        private void button17_Click(object sender, EventArgs e)
        {
            Drawthread.Abort();
            Chipthread.Abort();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked) speed = 3;
            else speed = 1;
        }

        private void button15_MouseDown(object sender, MouseEventArgs e)
        {
            if ((sender as Button).Name.Length == 8)
                key = int.Parse(string.Format("{0}{1}", (sender as Button).Name[6], (sender as Button).Name[7]));
            else
                key = int.Parse(string.Format("{0}", (sender as Button).Name[6]));
        }

        private void button15_MouseUp(object sender, MouseEventArgs e)
        {
            key = -1;
        }
    }

    public class CPU
    {
        public class GPU
        {
            public bool[,] screen;
            public bool DrawSprite(int x, int y, byte[] bytes)
            {
                bool result = false;
                for (int i = 0; i < bytes.Length; i++)
                {
                    System.Collections.BitArray a = new System.Collections.BitArray(BitConverter.GetBytes(bytes[i]));
                    for (int j = 0; j < 8; j++)
                    {
                        if (x+j < 64 && y+i < 32)
                        {
                            if (screen[x + j, y + i] == true && a[7 - j] == true)
                            {
                                screen[x + j, y + i] = false;
                                result = true;
                            }
                            else if (screen[x + j, y + i] == false && a[7 - j] == true)
                                screen[x + j, y + i] = a[7 - j];
                        }
                    }
                }
                return result;
            }
            public void ClearScreen()
            {
                screen = new bool[64, 32];
            }
            public GPU()
            {
                screen = new bool[64, 32];
            }
        }
        System.Timers.Timer timer;
        public GPU gpu;
        byte[] ram;
        int now; //Текущая позиция в памяти
        byte[] regs;
        int regi; //Регистр I (адресный)
        byte delaytimer, soundtimer;
        Stack<int> stack; //Стек
        public CPU()
        {
            ram = new byte[4096];
            stack = new Stack<int>();
            regs = new byte[16];
            now = 512;
            delaytimer = 0;
            soundtimer = 0;
            gpu = new GPU();
            //timer = new System.Timers.Timer(16.666666);
            timer = new System.Timers.Timer(2);
            timer.Elapsed += new ElapsedEventHandler(TimerTick);
            timer.Start();
        }
        public void TimerTick(object source, ElapsedEventArgs e)
        {
            if (delaytimer > 0) delaytimer--;
            if (soundtimer > 0) soundtimer--;
        }
        //Загружает программу из файла
        public void LoadProgram(string filename)
        {
            int pos = 512;
            byte[] a;
            BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open));
            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                a = reader.ReadBytes(2);
                ram[pos] = a[0];
                ram[pos + 1] = a[1];
                pos += 2;
            }
            reader.Close();
        }
        //Один такт работы
        public void Tick(int key)
        {
            byte[] ins = new byte[4]; //Текущая инструкция
            //Вычисляем инструкцию
            ins[0] = (byte)(ram[now] / 16);
            ins[1] = (byte)(ram[now] & 15);
            now++;
            ins[2] = (byte)(ram[now] / 16);
            ins[3] = (byte)(ram[now] & 15);
            now++;
            if (ins[0] == 0 && ins[3] == 0 && ins[2] == 14)
                gpu.ClearScreen();
            else if (ins[0] == 0 && ins[3] == 14 && ins[2] == 14)
                now = stack.Pop();
            else if (ins[0] == 1)
                now = ((ins[1] * 16 + ins[2]) * 16 + ins[3]);
            else if (ins[0] == 2)
            {
                stack.Push(now);
                now = ((ins[1] * 16 + ins[2]) * 16 + ins[3]);
            }
            else if (ins[0] == 3)
            {
                if (regs[ins[1]] == (ins[2] * 16 + ins[3]))
                    now += 2;
            }
            else if (ins[0] == 4)
            {
                if (regs[ins[1]] != (ins[2] * 16 + ins[3]))
                    now += 2;
            }
            else if (ins[0] == 5)
            {
                if (regs[ins[1]] == regs[ins[2]])
                    now += 2;
            }
            else if (ins[0] == 6)
            {
                regs[ins[1]] = (byte)(ins[2] * 16 + ins[3]);
            }
            else if (ins[0] == 7)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] + (ins[2] * 16 + ins[3]));
            }
            else if (ins[0] == 8 && ins[3] == 0)
            {
                regs[ins[1]] = regs[ins[2]];
            }
            else if (ins[0] == 8 && ins[3] == 1)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] | regs[ins[2]]);
            }
            else if (ins[0] == 8 && ins[3] == 2)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] & regs[ins[2]]);
            }
            else if (ins[0] == 8 && ins[3] == 3)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] ^ regs[ins[2]]);
            }
            else if (ins[0] == 8 && ins[3] == 4)
            {
                int res = (int)(regs[ins[1]] + regs[ins[2]]);
                if (res > 255) regs[15] = 1;
                else regs[15] = 0;
                regs[ins[1]] = (byte)(res & 255);
            }
            else if (ins[0] == 8 && ins[3] == 5)
            {
                int res = (int)(regs[ins[2]] - regs[ins[1]]);
                if (regs[ins[2]] >= regs[ins[1]]) regs[15] = 1;
                else regs[15] = 0;
                regs[ins[1]] = (byte)(res & 255);
            }
            else if (ins[0] == 8 && ins[3] == 6)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] >> 1);
                if ((regs[ins[2]] & 1) == 1) regs[15] = 1;
                else regs[15] = 0;
            }
            else if (ins[0] == 8 && ins[3] == 7)
            {
                if (regs[ins[2]] >= regs[ins[1]]) regs[15] = 1;
                else
                {
                    regs[15] = 0;
                    regs[ins[1]] = (byte)(regs[ins[1]] - regs[ins[2]]);
                }
            }
            else if (ins[0] == 8 && ins[3] == 14)
            {
                regs[ins[1]] = (byte)(regs[ins[1]] << 1);
                if ((regs[ins[2]] & 1) == 1) regs[15] = 1;
                else regs[15] = 0;
            }
            else if (ins[0] == 9)
            {
                if (regs[ins[1]] != regs[ins[2]])
                    now += 2;
            }
            else if (ins[0] == 10)
            {
                regi = ((ins[1] * 16 + ins[2]) * 16 + ins[3]);
            }
            else if (ins[0] == 11)
            {
                now = ((ins[1] * 16 + ins[2]) * 16 + ins[3]) + regs[0];
            }
            else if (ins[0] == 12)
            {
                Random rnd = new Random();
                regs[ins[1]] = (byte)(rnd.Next(0, 255) & ((ins[2] * 16 + ins[3])));
            }
            else if (ins[0] == 13)
            {
                byte[] spritee = new byte[ins[3]];
                for (int i = 0; i < ins[3]; i++)
                    spritee[i] = ram[regi + i];
                if (gpu.DrawSprite(regs[ins[1]], regs[ins[2]], spritee))
                    regs[15] = 1;
                else regs[15] = 0;
            }
            else if (ins[0] == 14 && ins[2] == 9 && ins[3] == 14)
            {
                if (key == regs[ins[1]])
                    now += 2;
            }
            else if (ins[0] == 14 && ins[3] == 1)
            {
                if (key != regs[ins[1]])
                    now += 2;
            }
            else if (ins[0] == 15 && ins[2] == 0 && ins[3] == 7)
            {
                regs[ins[1]] = delaytimer;
            }
            else if (ins[0] == 15 && ins[2] == 0 && ins[3] == 10)
            {
                if (key < 0) now -= 2;
            }
            else if (ins[0] == 15 && ins[2] == 1 && ins[3] == 5)
            {
                delaytimer = regs[ins[1]];
            }
            else if (ins[0] == 15 && ins[2] == 1 && ins[3] == 8)
            {
                soundtimer = regs[ins[1]];
            }
            else if (ins[0] == 15 && ins[2] == 1 && ins[3] == 14)
            {
                regi = regi + regs[ins[1]];
            }
            else if (ins[0] == 15 && ins[2] == 2 && ins[3] == 9)
            {
                //ЗАГЛУШКА
            }
            else if (ins[0] == 15 && ins[2] == 3 && ins[3] == 3)
            {
                //ЗАГЛУШКА
            }
            else if (ins[0] == 15 && ins[2] == 5 && ins[3] == 5)
            {
                //ЗАГЛУШКА
            }
            else if (ins[0] == 15 && ins[2] == 6 && ins[3] == 5)
            {
                //ЗАГЛУШКА
            }
        }
        public string GetDebug()
        {
            string a = "";
            a += string.Format("{0}\n",now);
            for (int i = 0; i < 16; i++)
            a += string.Format("V{0} = {0}\n", i, regs[i]);
            return a;
        }
    }
}