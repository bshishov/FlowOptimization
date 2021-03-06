﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FlowOptimization.Data;
using FlowOptimization.Data.Pipeline;
using FlowOptimization.Matrix;
using FlowOptimization.UI;
using FlowOptimization.UI.DataGridView;
using FlowOptimization.Utilities;
using FlowOptimization.Utilities.Canvas;
using FlowOptimization.Utilities.IO;
using OpenTK.Graphics.OpenGL;
using System.IO;
namespace FlowOptimization
{
    public partial class MainForm : Form
    {
        private bool _loaded = false;   // Форма пока что не загружена
        private int _mouseX;    // Координаты мыши по X
        private int _mouseY;    // Координаты мыши по Y
        private int _oldMouseX;
        private int _oldMouseY;
        /// <summary>
        /// Idle
        /// Selected - выбран какой-либо узел
        /// Draggable - процесс перетаскивания узла
        /// LineCreation - процесс создания линии
        /// NodeCreation - создание узла
        /// </summary>
        private enum States { Idle, Selected, Draggable, LineCreation, NodeCreation, ShowDialog };

        public enum Commands { NodeName, NodeVolume };  // Команды для диалоговых окон
        private States _state;

        private List<Node> _nodes;  // Список узлов
        private List<Pipe> _pipes;  // Список связей
        private Objects _objects;   // Объекты
        private List<ICV> _icvs;    // Список независимых поставщиков 
        private TextureInfo _background;    // Файл подложки
        private string _backgroundPath; // Путь к файлу подложки

        private Node _operatedNode = new Node();
        private Node _endNode = new Node();

        private IntersectionMatrix _intersectionMatrix; // Матрица пересечений
        private ICVsMatrix _icvsMatrix; // Матрица независимых поставщиков

        private DataTableView _dataTableView;
        
        private string _filePath;

        public MainForm()
        {
            InitializeComponent();            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Инициализируем объекты
            _objects = new Objects();
            _icvs = new List<ICV>();
            //_intersectionMatrix = new IntersectionMatrix(_nodes);
            label4.Text = "Н\nо\nм\ne\nр\n \nу\nз\nл\nа";
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            // Обновляем форму
            Refresh();

            // Форма загружена
            _loaded = true;
         
            // Настройки рисования
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Normalize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.DstAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            
            GL.ClearColor(Color.SkyBlue);
            SetupViewport();
            
            _state = States.Idle;
            _backgroundPath = null;      
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            //if (!_loaded)
              //  return;
        }

        /// <summary>
        /// Прорисовка всех объектов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!_loaded)
                return;

            // Если был создан хоть один узел, то можно производить расчеты
            if (_objects.GetNodes().Count > 0)
                матрицаРасстоянийToolStripMenuItem.Enabled = true;

            // Активируем элемент и очищаем буфер
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            // Рисуем подложку только после её подгрузки (когда получаем путь до файла)
            if (_backgroundPath != null)
                DrawingUtilities.DrawTexture(_background, 0, glControl1.Height, (float)glControl1.Height / _background.Height);

            // Получаем списки всех объектов
            _nodes = _objects.GetNodes();
            _pipes = _objects.GetPipes();

            // Рисуем связи
            foreach (var pipe in _pipes)
                DrawingUtilities.DrawLine(pipe);
            // Рисуем узлы
            foreach (var node in _nodes)
                DrawingUtilities.DrawRect(node);

            // Привязываем матрицу пересечений к dataGridView
            if (_nodes.Count != 0)
            {
                _intersectionMatrix = new IntersectionMatrix(_nodes);
                dataGridView1.DataSource = _intersectionMatrix.GetTable();
            }

            // Подсветка ячеек DataGridView
            DataTableUtilities.SetBackLight(dataGridView4, _nodes, Color.DodgerBlue);
            DataTableUtilities.SetBackLight(dataGridView5, _nodes, Color.DodgerBlue);
            DataTableUtilities.SetBackLight(dataGridView8, _nodes, Color.DodgerBlue);
            DataTableUtilities.SetBackLight(dataGridView9, _nodes, Color.DodgerBlue);

            DataTableUtilities.SetBackLight(dataGridView1, Color.Gold);
            DataTableUtilities.SetBackLight(dataGridView2, Color.Gold);
            DataTableUtilities.SetBackLight(dataGridView4, Color.Gold);
            DataTableUtilities.SetBackLight(dataGridView5, Color.Gold);
            DataTableUtilities.SetBackLight(dataGridView8, Color.Gold);
            DataTableUtilities.SetBackLight(dataGridView9, Color.Gold);

            // Если выделен какой-либо узел
            if (_state == States.Selected)
            {
                // Рисуем обводку
                DrawingUtilities.DrawGrid(_operatedNode);
                // В матрице пересечений подсвечиваем записи о узлах с которыми он соединен
                DataTableUtilities.SetBackLight(dataGridView1, _operatedNode, Color.Red);
            }
            else if (_state == States.LineCreation)
                DrawingUtilities.DrawLineByMouse(_operatedNode, _mouseX, _mouseY);
        
            // Нумеруем заголовочные столбцы
            DataTableUtilities.SetRowNumber(dataGridView1);
            DataTableUtilities.SetRowNumber(dataGridView2);
            DataTableUtilities.SetRowNumber(dataGridView3);
            DataTableUtilities.SetRowNumber(dataGridView4);
            DataTableUtilities.SetRowNumber(dataGridView5);
            DataTableUtilities.SetRowNumber(dataGridView8);
            DataTableUtilities.SetRowNumber(dataGridView9);

            // Привязыаем список узлов и связей к objectListView
            objectListView2.SetObjects(_pipes);
            objectListView1.SetObjects(_nodes);
            // Обновляем значения 
            objectListView1.Refresh();
            objectListView2.Refresh();
            
            GL.Flush();
            GL.Finish();
            glControl1.SwapBuffers();          
        }

        /// <summary>
        /// Стандартные настройки glControl1
        /// </summary>
        private void SetupViewport()
        {
            int w = glControl1.Width;
            int h = glControl1.Height;
            GL.MatrixMode(MatrixMode.Projection);
            GL.Viewport(0, 0, w, h);
            GL.Ortho(0, w, 0, h, -1, 1);
        }

        private void glControl1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _mouseX = e.X;
            _mouseY = glControl1.Height - e.Y;

            // Меняем координаты узла на текущие координаты мыши
            if (_state == States.Draggable)
            {
                _operatedNode.X = _mouseX;
                _operatedNode.Y = _mouseY;
            }
            else if (_state == States.ShowDialog && _oldMouseX != _mouseX && _oldMouseY != _mouseY)
                _state = States.LineCreation;

            if(_loaded)
                glControl1_Paint(this, null);
        }

        private void glControl1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        { 
            _state = States.Idle;

            // Проверяем попала ли мышь в какой-либо из узлов
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _state = States.Draggable;
                        _operatedNode = node;

                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        _oldMouseX = _mouseX;
                        _oldMouseY = _mouseY;

                        _state = States.ShowDialog;
                        _operatedNode = node;
                    }
                    else if (e.Button == MouseButtons.Middle)
                    {
                        _state = States.LineCreation;
                        _operatedNode = node;
                    }
                }                     
            }
        }

        private void glControl1_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_state == States.Draggable)
            {
                _state = States.Selected;
                glControl1_Paint(this, null);
            }
            else if (_state == States.LineCreation)
            {
                foreach (var node in _nodes)
                {
                    if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                        && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y && _oldMouseX != _mouseX && _oldMouseY != _mouseY)
                    {
                        _endNode = node;
                        // Проверяем существует ли уже такая связь и не являются ли начальный и конечный узлы одним и тем же узлом
                        if (!(_endNode.ConnectedNodes.Contains(_operatedNode)) && _operatedNode != _endNode)
                            _objects.AddPipe(_operatedNode, _endNode);
                        glControl1.Invalidate();
                        _state = States.Idle;
                    }
                    else
                    {
                        _state = States.Idle;
                        glControl1_Paint(this, null);
                    }
                }
            }
            else if (_state == States.NodeCreation)
            {
                _objects.AddNode(_mouseX, _mouseY);
                _state = States.Idle;
                glControl1_Paint(this, null);
            }
            // Вызываем контекстное меню
            else if (_mouseX == _oldMouseX && _mouseY == _oldMouseY)
            {
                _state = States.Idle;
                contextMenuStrip1.Show(Cursor.Position);     
            }

            Refresh();
        }

        private void glControl1_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (!(_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y) && _state == States.Idle)
                {
                    _state = States.NodeCreation;
                }
            }
            if (_nodes.Count == 0)
            {
                _state = States.NodeCreation;
            }         
            glControl1_Paint(this,null);
        }

        private void входнойToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    var ef = new EditingForm(Commands.NodeVolume);
                    ef.ShowDialog();
                    if (ef.TextBoxValue != null)
                    {
                        node.NodeType = Node.Type.Enter;
                        node.Volume = Convert.ToInt32(ef.TextBoxValue);
                    }
                    else
                        MessageBox.Show(@"Необходимо ввести объем для данного узла!");

                    glControl1.Invalidate();
                }
            }
        }

        private void выходнойToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    
                    var ef = new EditingForm(Commands.NodeVolume);
                    ef.ShowDialog();
                    if (ef.TextBoxValue != null)
                    {
                        node.NodeType = Node.Type.Exit;
                        node.Volume = Convert.ToInt32(ef.TextBoxValue);
                    }                  
                    else
                        MessageBox.Show(@"Необходимо ввести объем для данного узла!");
                        
                    glControl1.Invalidate();
                }
            }
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    _operatedNode = node;
                    _objects.DeleteNode(_operatedNode);
                    glControl1.Invalidate();
                    break;
                }
            }
        }

        private void названиеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    var ef = new EditingForm(Commands.NodeName);
                    ef.ShowDialog();
                    if (ef.TextBoxValue != null)
                        node.Name = ef.TextBoxValue;
                    glControl1.Invalidate();
                }
            }
        }

        private void объемToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var node in _nodes)
            {
                if (_mouseX <= (node.X + DrawingUtilities.RectSide) && _mouseX >= (node.X)
                    && _mouseY <= (node.Y + DrawingUtilities.RectSide) && _mouseY >= node.Y)
                {
                    var ef = new EditingForm(Commands.NodeVolume);
                    ef.ShowDialog();
                    if (ef.TextBoxValue != null)
                    {
                        node.Volume = Convert.ToInt32(ef.TextBoxValue);
                        node.NodeType = Node.Type.Exit;
                    }
                        
                    glControl1.Invalidate();
                }
            }
        }

        private void сохранитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var data = new CSVExport(_nodes, _pipes, _icvs);
            data.ExportToFile(_filePath);
        }

        private void открытьToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            FileDialog dialog = new OpenFileDialog();
            dialog.Filter = @"Текстовые документы|*.txt|Все файлы (*.*)|*.*";
            dialog.ShowDialog();

            _filePath = dialog.FileName;

            if (!String.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            {
                _objects.ResetIDs();
                // Очищаем все списки перед импортом новых данных
                _nodes.Clear();
                _pipes.Clear();
                _icvs.Clear();
                var import = new CSVImport(_filePath);

                import.Import(ref _objects, ref _icvs);
                _nodes = _objects.GetNodes();
                _pipes = _objects.GetPipes();

                if (_icvs.Count != 0)
                {
                    _icvsMatrix = new ICVsMatrix(_icvs);
                    dataGridView6.DataSource = _icvsMatrix.GetTable();
                }

            }
            else
                _filePath = null; 
                
            glControl1.Invalidate();
        }

        private void сохранитьВToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileDialog dialog = new SaveFileDialog();
            dialog.Filter = @"Текстовые документы|*.txt|Все файлы (*.*)|*.*";
            dialog.ShowDialog();

            _filePath = dialog.FileName;

            if (!String.IsNullOrEmpty(_filePath))
            {
                var data = new CSVExport(_nodes, _pipes, _icvs);
                data.ExportToFile(_filePath);
            }
            else
                _filePath = null;
        }

        private void матрицаРасстоянийToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Обнуляем общую ТТР для каждого узла
            foreach (var node in _nodes)
                node.Ttr = 0;
   
            _dataTableView = new DataTableView(_intersectionMatrix, _nodes, _icvsMatrix);

            DataGridContent.BuildContent(dataGridView2, _dataTableView.DistanceMatrix);
            //dataGridView2.DataSource = _dataTableView.DistanceMatrix;
            dataGridView3.DataSource = _dataTableView.RoutesMatrix;
            dataGridView4.DataSource = _dataTableView.DistributionMatrix;
            dataGridView5.DataSource = _dataTableView.TtrMatrix;
            dataGridView8.DataSource = _dataTableView.IcvDistributionMatrix;
            dataGridView9.DataSource = _dataTableView.IcvTtrMatrix;

           

            comboBox1.Enabled = true;
        }

        private void выходToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void загрузитьПодложкуToolStripMenuItem_Click(object sender, EventArgs e)
        {   
            FileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
            _backgroundPath = dialog.FileName;

            if (!String.IsNullOrEmpty(_backgroundPath) && File.Exists(_backgroundPath))
                _background = DrawingUtilities.LoadTexture(_backgroundPath, false);
            else
                _backgroundPath = null;

            glControl1.Invalidate();
        }

        private void загрузитьПодлужкуБезРастяжкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _objects.ResetIDs();
            _nodes.Clear();
            _pipes.Clear();
        }

        private void оПрограммеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var progInfoWindow = new AboutForm();
            progInfoWindow.ShowDialog();
        }

        private void fAQToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("Penguins.jpg");
        }

        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            glControl1.Invalidate();
        }

        private void независимыйПоставщикToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var icvForm = new ICVForm(_objects);
            icvForm.ShowDialog();
            if (icvForm.Icv != null)
            {
                _icvs.Add(icvForm.Icv);
                _icvsMatrix = new ICVsMatrix(_icvs);
                dataGridView6.DataSource = _icvsMatrix.GetTable();
            }
        }

        private void contextMenuStrip1_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            _oldMouseX = _mouseX;
            _oldMouseY = _mouseY;
        }

        private void матрицаРасстоянийСНПToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Обнуляем общую ТТР для каждого узла
            foreach (var node in _nodes)
                node.Ttr = 0;

            //SolveGraph();

           /* dataGridView2.DataSource = _uiDistanceMatrix;
            dataGridView3.DataSource = _uiRoutesMatrix;
            dataGridView4.DataSource = _uiDistributionMatrix;
            dataGridView5.DataSource = _uiTTRMatrix;
            dataGridView8.DataSource = _distributionMatrix.GetTable(_routesMatrix, _icvsMatrix);*/

            comboBox1.Enabled = true;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedItem = comboBox1.SelectedIndex;
            dataGridView7.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;

            switch (selectedItem)
            {
                case 0:
                    dataGridView7.DataSource = _intersectionMatrix.GetTable();
                    break;
                case 1:
                    dataGridView7.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.ColumnHeader;
                    dataGridView7.DataSource = _icvsMatrix.GetTable();
                    break;
                case 2:
                    //dataGridView7.DataSource = _uiDistanceMatrix;
                    break;
                case 3:
                    dataGridView7.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.ColumnHeader;
                   // dataGridView7.DataSource = _uiRoutesMatrix;
                    break;
                case 4:
                    //dataGridView7.DataSource = _uiDistributionMatrix;
                    break;
                case 5:
                   // dataGridView7.DataSource = _uiTTRMatrix;
                    break;
            }

            DataTableUtilities.SetRowNumber(dataGridView7);
            glControl1_Paint(this, null);
        }
    }
}
