Imports System.IO
Public Class FrmMain
    Dim Offset As Integer
    Dim Opened_File() As Byte
    Dim File_Name As String
    Dim Has_Open_File As Boolean

    Dim Plain_Method As Boolean
    Private Sub FrmMain_KeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.KeyCode = Keys.M Then 'Maximiza
            Me.MaximumSize = Screen.PrimaryScreen.WorkingArea.Size
            If Me.WindowState = FormWindowState.Maximized Then Me.WindowState = FormWindowState.Normal Else Me.WindowState = FormWindowState.Maximized
        End If

        If Has_Open_File Then
            Dim sName As String = Path.GetFileName(File_Name)
            Dim Input_Files() As FileInfo = New DirectoryInfo(Path.GetDirectoryName(File_Name)).GetFiles()
            Select Case e.KeyCode
                Case Keys.Left
                    For Index As Integer = 1 To Input_Files.Count - 1
                        If Input_Files(Index).Name = sName Then
                            Dim i As Integer = 1
                            Do
                                Dim CurrFile As String = Input_Files(Index - i).FullName
                                If Path.GetExtension(CurrFile).ToLower = ".ctxr" Then
                                    Open(CurrFile)
                                    Exit Sub
                                End If
                                i += 1
                                If Index - i < 1 Then Exit For
                            Loop
                            Exit For
                        End If
                    Next
                Case Keys.Right
                    For Index As Integer = 0 To Input_Files.Count - 2
                        If Input_Files(Index).Name = sName Then
                            Dim i As Integer = 1
                            Do
                                Dim CurrFile As String = Input_Files(Index + i).FullName
                                If Path.GetExtension(CurrFile).ToLower = ".ctxr" Then
                                    Open(CurrFile)
                                    Exit Sub
                                End If
                                i += 1
                                If Index + i > Input_Files.Count - 2 Then Exit Sub
                            Loop
                            Exit For
                        End If
                    Next
            End Select
        End If
    End Sub

    Private Sub BtnOpen_Click(sender As Object, e As EventArgs) Handles BtnOpen.Click
        Dim OpenDlg As New OpenFileDialog
        OpenDlg.Filter = "Textura do MGS3|*.ctxr"
        If OpenDlg.ShowDialog = DialogResult.OK AndAlso File.Exists(OpenDlg.FileName) Then
            Open(OpenDlg.FileName)
            PicPanel.Focus()
        End If
    End Sub
    Private Sub Open(FileName As String)
        Has_Open_File = True
        File_Name = FileName
        Opened_File = File.ReadAllBytes(FileName)
        Dim Width As Integer = (Opened_File(&H2C) * &H100) Or Opened_File(&H2D)
        Dim Height As Integer = (Opened_File(&H2E) * &H100) Or Opened_File(&H2F)

        Plain_Method = (IsPowerOfTwo(Width) = False) Or (IsPowerOfTwo(Height) = False)
        Dim Bmp As New Bitmap(Width, Height)
        Offset = &H80
        If Plain_Method Then
            For Y As Integer = 0 To Height - 1
                For X As Integer = 0 To Width - 1
                    Bmp.SetPixel(X, Y, Color.FromArgb(Opened_File(Offset), _
                                                      Opened_File(Offset + 1), _
                                                      Opened_File(Offset + 2), _
                                                      Opened_File(Offset + 3)))
                    Offset += 4
                Next
            Next
        Else
            Dim Min_Size As Integer = Math.Min(Width, Height)
            For Y As Integer = 0 To Height - (Min_Size - 1) Step Min_Size
                For X As Integer = 0 To Width - (Min_Size - 1) Step Min_Size
                    Decode_Tile(Min_Size, Min_Size, X, Y, Bmp, Opened_File)
                Next
            Next
        End If
        Preview.Image = Bmp
        LblTitle.Text = "MilkGear - " & Path.GetFileNameWithoutExtension(File_Name)
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs) Handles BtnSave.Click
        If Not Has_Open_File Then Exit Sub

        Dim Min_Size As Integer = Math.Min(Preview.Image.Width, Preview.Image.Height)
        Dim Bmp As New Bitmap(Preview.Image)
        Dim Out(((Preview.Width * Preview.Height) * 4) + &H7F) As Byte
        Buffer.BlockCopy(Opened_File, 0, Out, 0, &H80)
        Out(&H2C) = (Preview.Image.Width >> 8) And &HFF
        Out(&H2D) = Preview.Image.Width And &HFF
        Out(&H2E) = (Preview.Image.Height >> 8) And &HFF
        Out(&H2F) = Preview.Image.Height And &HFF
        Offset = &H80
        If Plain_Method Then
            For Y As Integer = 0 To Preview.Image.Height - 1
                For X As Integer = 0 To Preview.Image.Width - 1
                    Dim Col As Color = Bmp.GetPixel(X, Y)
                    Out(Offset) = Col.A
                    Out(Offset + 1) = Col.R
                    Out(Offset + 2) = Col.G
                    Out(Offset + 3) = Col.B
                    Offset += 4
                Next
            Next
        Else
            For Y As Integer = 0 To Preview.Image.Height - 1 Step Min_Size
                For X As Integer = 0 To Preview.Image.Width - 1 Step Min_Size
                    Encode_Tile(Min_Size, Min_Size, X, Y, Bmp, Out)
                Next
            Next
        End If

        File.WriteAllBytes(File_Name, Out)
        'MessageBox.Show("O arquivo foi alterado com sucesso!", "Feito", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub BtnExport_Click(sender As Object, e As EventArgs) Handles BtnExport.Click
        If Not Has_Open_File Then Exit Sub

        Dim SaveDlg As New SaveFileDialog
        SaveDlg.Filter = "Imagem|*.png"
        SaveDlg.FileName = Path.GetFileNameWithoutExtension(File_Name)
        If SaveDlg.ShowDialog = DialogResult.OK Then
            Preview.Image.Save(SaveDlg.FileName)
        End If
    End Sub
    Private Sub BtnImport_Click(sender As Object, e As EventArgs) Handles BtnImport.Click
        If Not Has_Open_File Then Exit Sub

        Dim OpenDlg As New OpenFileDialog
        OpenDlg.Filter = "Imagem|*.png;*.bmp;*.gif;*.jpg;*.tiff"
        If OpenDlg.ShowDialog = DialogResult.OK AndAlso File.Exists(OpenDlg.FileName) Then
            Dim Img As New Bitmap(OpenDlg.FileName)
            Preview.Image = Img
        End If
    End Sub

    Private Sub Decode_Tile(Total_Size As Integer, Tile_Size As Integer, PX As Integer, PY As Integer, Bmp As Bitmap, Data() As Byte)
        If Tile_Size = 0 Then
            Bmp.SetPixel(PX, PY, Color.FromArgb(Data(Offset), Data(Offset + 1), Data(Offset + 2), Data(Offset + 3)))
            Offset += 4
        Else
            Dim Y As Integer
            While Y < Total_Size
                Dim X As Integer = 0
                While X < Total_Size
                    Decode_Tile(Tile_Size, Tile_Size \ 2, X + PX, Y + PY, Bmp, Data)
                    X += Tile_Size
                End While
                Y += Tile_Size
            End While
        End If
    End Sub
    Private Sub Encode_Tile(Total_Size As Integer, Tile_Size As Integer, PX As Integer, PY As Integer, Bmp As Bitmap, Data() As Byte)
        If Tile_Size = 0 Then
            Dim Col As Color = Bmp.GetPixel(PX, PY)
            Data(Offset) = Col.A
            Data(Offset + 1) = Col.R
            Data(Offset + 2) = Col.G
            Data(Offset + 3) = Col.B
            Offset += 4
        Else
            Dim Y As Integer
            While Y < Total_Size
                Dim X As Integer = 0
                While X < Total_Size
                    Encode_Tile(Tile_Size, Tile_Size \ 2, X + PX, Y + PY, Bmp, Data)
                    X += Tile_Size
                End While
                Y += Tile_Size
            End While
        End If
    End Sub

    Private Function IsPowerOfTwo(Input As ULong) As Boolean
        Return (Input <> 0) AndAlso ((Input And (Input - 1)) = 0)
    End Function

    ' Existing code remains the same until the end of the class...

    ' Add these new methods for command-line processing
    Public Shared Sub Main()
        Dim args As String() = Environment.GetCommandLineArgs()
        ' Handle help request first
        If args.Length > 1 AndAlso args(1).ToLower() = "-h" Then
            ShowUsage()
            Exit Sub
        End If
		
        If args.Length >= 3 Then
            Select Case args(1).ToLower()
                Case "-e"
                    If Directory.Exists(args(2)) Then
                        ProcessFolderExport(args(2))
                    Else
                        ExportFromCommandLine(args(2))
                    End If
                Case "-i"
                    If Directory.Exists(args(2)) Then
                        ProcessFolderImport(args(2))
                    Else
                        ImportFromCommandLine(args(2))
                    End If
                Case Else
                    ShowUsage()
            End Select
        Else
            Application.Run(New FrmMain())
        End If
    End Sub

    Private Shared Sub ExportFromCommandLine(ctxrFile As String)
        If Not File.Exists(ctxrFile) Then
            Console.WriteLine("File not found: " & ctxrFile)
            Exit Sub
        End If

        Dim instance As New FrmMain()
        instance.Open(ctxrFile)
        Dim pngFile As String = Path.ChangeExtension(ctxrFile, ".png")
        instance.Preview.Image.Save(pngFile)
        Console.WriteLine("Exported to: " & pngFile)
    End Sub

    Private Shared Sub ImportFromCommandLine(pngFile As String)
        If Not File.Exists(pngFile) Then
            Console.WriteLine("File not found: " & pngFile)
            Exit Sub
        End If

        Dim ctxrFile As String = Path.ChangeExtension(pngFile, ".ctxr")
        If File.Exists(ctxrFile) Then
            Dim instance As New FrmMain()
            instance.Open(ctxrFile)
            instance.ImportImage(pngFile)
            instance.BtnSave_Click(instance, EventArgs.Empty)
            Console.WriteLine("Imported to: " & ctxrFile)
        Else
            Console.WriteLine("Corresponding .ctxr file not found: " & ctxrFile)
        End If
    End Sub

    Private Sub ImportImage(pngFile As String)
        Dim Img As New Bitmap(pngFile)
        Preview.Image = Img
    End Sub
	


    Private Shared Sub ProcessFolderExport(folderPath As String)
        Dim ctxrFiles = Directory.GetFiles(folderPath, "*.ctxr", SearchOption.AllDirectories)
        Console.WriteLine($"Found {ctxrFiles.Length} .ctxr files to process")

        For Each file In ctxrFiles
            Try
                ExportFromCommandLine(file)
            Catch ex As Exception
                Console.WriteLine($"Error processing {file}: {ex.Message}")
            End Try
        Next

        Console.WriteLine("Export completed")
    End Sub

    Private Shared Sub ProcessFolderImport(folderPath As String)
        Dim pngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories)
        Console.WriteLine($"Found {pngFiles.Length} .png files to process")

        For Each file In pngFiles
            Try
                ImportFromCommandLine(file)
            Catch ex As Exception
                Console.WriteLine($"Error processing {file}: {ex.Message}")
            End Try
        Next

        Console.WriteLine("Import completed")
    End Sub

    Private Shared Sub ShowUsage()
        Console.WriteLine("Usage:")
		Console.WriteLine()
        Console.WriteLine("  Batch commands, Below commands will Go through the Given Folder & Subfolders to Export the PNG or Import them into the CTXR")
		Console.WriteLine("  milkgear.exe -e folder   Export CTXR to PNG")
        Console.WriteLine("  milkgear.exe -i folder    Import PNG to CTXR")
		Console.WriteLine()
        Console.WriteLine("  Single Line Commands")
        Console.WriteLine("  milkgear.exe -e file.ctxr    Export to PNG")
        Console.WriteLine("  milkgear.exe -i file.png     Import from PNG")
    End Sub
End Class
