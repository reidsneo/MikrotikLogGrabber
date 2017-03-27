Imports Renci.SshNet
Imports System.IO
Imports Renci.SshNet.Sftp
Imports System.Net.Mime.MediaTypeNames
Imports System.Threading
Imports System.Data
Imports MySql.Data.MySqlClient
Imports System.Threading.Tasks

Module Main
    Dim fileSize As Long
    Dim curpath As String = My.Application.Info.DirectoryPath
    Private _dbhost As String = "192.168.87.71"
    Private _dbuser As String = "root"
    Private _dbpass As String = "toor"
    Dim taskCount As Integer = 0
    Dim taskMin As Integer = 1
    Dim taskLimit As Integer = 3
    '=================================
    Dim tableloggrab As String = "tool_mikrotik_loggrab"
    '=================================
    Dim ThreadCount, ThreadPorts As Integer

    Function Logfetch(ByVal busip As String, ByVal busid As String)
        Dim isonline, islogenable As Integer
        Dim errmsg As String = ""
        'Flag the record is processed
        UpdateFlag(busid)
        Try
            If (Not System.IO.Directory.Exists(curpath & "\" & busid)) Then
                System.IO.Directory.CreateDirectory(curpath & "\" & busid)
            End If
            Using sftp As New SftpClient(busip, 22, "admin", "k0nijn")
                Dim SshClient = New SshClient(busip, 22, "admin", "k0nijn")
                sftp.Connect()
                sftp.ChangeDirectory("/")
                Dim cmd As SshCommand
                SshClient.Connect()

                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Dis] - Uploading script to " & busid)
                Dim fs As System.IO.Stream = System.IO.File.OpenRead("logging.rsc")
                sftp.UploadFile(fs, "/logging.rsc", True)
                fs.Close()
                'EXECUTE SCRIPT FILE
                cmd = SshClient.CreateCommand("import file-name=logging.rsc")
                cmd.Execute()
                'Console.WriteLine(cmd.Result)
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Ena] - Script run success on " & busid)

                'LISTING DIR
                Dim lofListing As List(Of SftpFile) = sftp.ListDirectory(".").ToList()
                Dim i As Integer
                If lofListing.Count = 0 Then
                    isonline = 1
                    UpdateData(busid, isonline, islogenable, errmsg)
                    'Console.WriteLine("Router not configured")
                    'PREPARE SCRIPT FILE
                    'UPLOAD SCRIPT FILE
                  
                Else
                    isonline = 1
                    For i = 0 To lofListing.Count - 1
                        islogenable = 1
                        Dim items As String = lofListing.Item(i).Name
                        If items.Contains(".txt") And items.Contains("V") Then
                            Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Get] - " & lofListing.Item(i).Name)
                            Using ms As New MemoryStream
                                'download as memory stream
                                'sftp.DownloadFile(lofListing.Item(i).FullName, ms, AddressOf DownloadCallback) 'with download progress
                                sftp.DownloadFile(lofListing.Item(i).FullName, ms) 'without download progress

                                'here we try an asynchronous operation and wait for it to complete.
                                Dim asyncr As IAsyncResult = sftp.BeginDownloadFile(lofListing.Item(i).FullName, ms)
                                'Dim sftpAsyncr As SftpDownloadAsyncResult = CType(asyncr, SftpDownloadAsyncResult)
                                'While Not sftpAsyncr.IsCompleted
                                '    Dim pct As Integer = CInt((sftpAsyncr.DownloadedBytes / fileSize) * 100)
                                '    Console.Write("{0} of {1} ({2}%).", sftpAsyncr.DownloadedBytes, fileSize, pct)
                                'End While
                                sftp.EndDownloadFile(asyncr)

                                'create a file stream
                                Dim localFileName As String = Date.Now.ToString("yyyyddMM") & "_" & lofListing.Item(i).Name
                                Dim fls As New FileStream(curpath & "\" & busid & "\" & localFileName, FileMode.Create, FileAccess.Write)
                                'write the memory stream to the file stream
                                ms.WriteTo(fls)

                                'close file stream
                                fls.Close()

                                'close memory stream
                                ms.Close()
                            End Using
                        End If
                        If taskCount = 0 Then
                            Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Fin] - All task done, reschedule all task")
                            ResetData()
                        ElseIf taskCount = taskMin Then
                            Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Res] - Minimum schedule reached, loading new data")
                            LoadNewData()
                        End If
                    Next i
                    UpdateData(busid, isonline, islogenable, errmsg)
                End If
            End Using
        Catch ex As Exception
            If ex.ToString.Contains("A connection attempt failed") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Off] - Bus " & busid & " was offline")
                isonline = 0
            ElseIf ex.ToString.Contains("Bad packet length") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Err] - Bad Packet " & busid & " internet unstable")
                errmsg = "Bad packet length"
            ElseIf ex.ToString.Contains("Session operation has timed out") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Err] - Sess Timeout " & busid & " internet unstable")
                errmsg = "Session operation has timed out"
            ElseIf ex.ToString.Contains("No connection could be made because the target machine actively refused it") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Err] - Target machine actively refused it " & busid)
                errmsg = "Target machine actively refused it"
            ElseIf ex.ToString.Contains("Permission denied(password)") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Err] - Permission denied(password) " & busid)
                errmsg = "Permission denied(password)"
            ElseIf ex.ToString.Contains("timeout ") Or ex.ToString.Contains("timed") Then
                errmsg = "Operation timeout"
            ElseIf ex.ToString.Contains("Client not connected") Then
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Off] - Bus " & busid & " was offline")
                isonline = 0
            Else
                Console.WriteLine("Process error: {0}", ex.ToString())
            End If
        End Try

        UpdateData(busid, isonline, islogenable, errmsg)

        'check task repetition
        taskCount -= 1

        'if all task complete, restart again
        If taskCount = 0 Then
            Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Fin] - All task done, reschedule all task")
            ResetData()
        ElseIf taskCount = taskMin Then
            Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Res] - Minimum schedule reached, loading new data")
            LoadNewData()
        End If
        'Console.WriteLine(taskCount)
    End Function

    Sub DownloadCallback()

    End Sub


    Sub PoolInfo()
        Dim workerThreads As Integer
        Dim completionPortThreads As Integer

        ThreadPool.GetAvailableThreads(workerThreads,
        completionPortThreads)
        Console.WriteLine("Available threads: {0}, async I/O: {1}",
        workerThreads, completionPortThreads)
        Console.ReadLine()
    End Sub

    Sub UpdateFlag(ByVal busid As String)
        Dim connstring As String = "server=" & _dbhost & ";userid=" & _dbuser & ";password=" & _dbpass & ";database=mnp_rack"
        Dim conn As MySqlConnection = Nothing
        Try
            conn = New MySqlConnection(connstring)
            conn.Open()
            Dim query As String = "UPDATE `mnp_rack`.`tool_mikrotik_loggrab` SET `isrunning` = '1',`num_chk` = `num_chk`+1,`lastcheck` = '" & DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") & "' WHERE `bus_id` = '" & busid & "'"
            Dim com As MySqlCommand = New MySqlCommand(query, conn)
            com.ExecuteNonQuery()
        Catch e As Exception
            Console.WriteLine("Error: {0}", e.ToString())
        Finally
            If conn IsNot Nothing Then
                conn.Close()
            End If
        End Try
    End Sub

    Sub IgnoreFlag(ByVal busid As String)
        Dim connstring As String = "server=" & _dbhost & ";userid=" & _dbuser & ";password=" & _dbpass & ";database=mnp_rack"
        Dim conn As MySqlConnection = Nothing
        Try
            conn = New MySqlConnection(connstring)
            conn.Open()
            Dim query As String = "UPDATE `mnp_rack`.`tool_mikrotik_loggrab` SET `isrunning` = '1',`lastcheck` = '" & DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") & "' WHERE `bus_id` = '" & busid & "'"
            Dim com As MySqlCommand = New MySqlCommand(query, conn)
            com.ExecuteNonQuery()
        Catch e As Exception
            Console.WriteLine("Error: {0}", e.ToString())
        Finally
            If conn IsNot Nothing Then
                conn.Close()
            End If
        End Try
    End Sub

    Sub UpdateData(ByVal busid As String, ByVal lastonline As String, ByVal islogenable As String, ByVal errmsg As String)
        Dim connstring As String = "server=" & _dbhost & ";userid=" & _dbuser & ";password=" & _dbpass & ";database=mnp_rack"
        Dim conn As MySqlConnection = Nothing
        Dim state, lastlogin, msgerr As String
        Try
            conn = New MySqlConnection(connstring)
            conn.Open()
            If lastonline = 1 Then
                state = "ON"
                lastlogin = ",`lastbusonline` = '" & DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") & "'"
            Else
                state = "OFF"
                lastlogin = ""
            End If

            If errmsg <> "" Then
                msgerr = ",`lasterrmsg` = '" & errmsg & "'"
            Else
                msgerr = ""
            End If


            Dim query As String = "UPDATE `mnp_rack`.`tool_mikrotik_loggrab` SET `state` = '" & state & "' , `islogenable` = '" & islogenable & "' " & lastlogin & msgerr & "  WHERE `bus_id` = '" & busid & "'"
            Dim com As MySqlCommand = New MySqlCommand(query, conn)
            com.ExecuteNonQuery()
        Catch e As Exception
            Console.WriteLine("Error: {0}", e.ToString())
        Finally
            If conn IsNot Nothing Then
                conn.Close()
            End If
        End Try
    End Sub

    Sub ResetData()
        Dim connstring As String = "server=" & _dbhost & ";userid=" & _dbuser & ";password=" & _dbpass & ";database=mnp_rack"
        Dim conn As MySqlConnection = Nothing
        Try
            conn = New MySqlConnection(connstring)
            conn.Open()
            Dim query As String = "UPDATE `mnp_rack`.`tool_mikrotik_loggrab` SET `isrunning` = '0'"
            Dim com As MySqlCommand = New MySqlCommand(query, conn)
            com.ExecuteNonQuery()
        Catch e As Exception
            Console.WriteLine("Error: {0}", e.ToString())
        Finally
            If conn IsNot Nothing Then
                conn.Close()
            End If
        End Try
    End Sub

    Sub LoadNewData()
        Dim connstring As String = "server=" & _dbhost & ";userid=" & _dbuser & ";password=" & _dbpass & ";database=mnp_rack"
        Dim conn As MySqlConnection = Nothing
        Try
            conn = New MySqlConnection(connstring)
            conn.Open()

            Dim query As String = "SELECT * FROM tool_mikrotik_loggrab WHERE isrunning='0' ORDER BY bus_id DESC LIMIT " & taskLimit & ";"
            Dim da As New MySqlDataAdapter(query, conn)
            Dim ds As New DataSet()
            da.Fill(ds, "tool_mikrotik_loggrab")
            Dim dt As DataTable = ds.Tables("tool_mikrotik_loggrab")

            'assign new task counter
            taskCount = dt.Rows.Count
            Dim ignoredcount As Integer = 0
            If dt.Rows.Count > 0 Then
                For Each row As DataRow In dt.Rows
                    Dim lastcheck, lastonline As String
                    lastcheck = Date.Now
                    lastonline = ""
                    For Each col As DataColumn In dt.Columns
                        'Console.Write(row(col).ToString() + vbTab)
                        lastcheck = row(8).ToString()
                        lastonline = row(9).ToString()
                    Next
                    If lastonline = "" Then
                        lastonline = "0000-00-00 00:00:00"
                    End If
                    If lastcheck = "" Then
                        lastcheck = Date.Now
                    End If
                    Dim elapsedTimeChk As TimeSpan = DateTime.Parse(DateTime.Parse(Date.Now)).Subtract(lastcheck)
                    Dim elapsedMinutesTextChk As String = elapsedTimeChk.Minutes.ToString() & "min ago"

                    Dim totalhour As String
                    Dim totalminutes As String
                    Dim texthour As String
                    If lastonline <> "0000-00-00 00:00:00" Then
                        Dim elapsedTimeOl As TimeSpan = DateTime.Parse(DateTime.Parse(Date.Now)).Subtract(lastonline)
                        Dim elapsedMinutesTextOl As String = elapsedTimeOl.TotalMinutes.ToString()
                        totalhour = elapsedTimeOl.TotalHours.ToString()
                        totalminutes = elapsedTimeOl.TotalMinutes.ToString()
                    Else
                        totalhour = "999"
                        totalminutes = "999"
                    End If

                    If totalhour = "999" Then
                        texthour = "New Scan"
                    Else
                        texthour = Conversion.Int(totalhour) & "hr ago"
                    End If

                    If totalminutes > 300 Then
                        Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Dbo] - Loaded " & row(0).ToString() & " [LastChk] " & elapsedMinutesTextChk & " [LastOL] " & texthour & " [Process]")
                        Threading.ThreadPool.QueueUserWorkItem(Function() Logfetch(row(2).ToString(), row(0).ToString()))
                    Else
                        IgnoreFlag(row(0).ToString())
                        Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Dbo] - Loaded " & row(0).ToString() & " [LastChk] " & elapsedMinutesTextChk & " [LastOL] " & texthour & " [Ignored]")
                        ignoredcount = ignoredcount + 1
                    End If
                    'lastcheck = row(1).ToString()
                Next
                If ignoredcount > 1 Then
                    Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Rel] - Too much ignored load new data")
                    LoadNewData()
                End If
            Else
                Console.WriteLine("[" & Date.Now.ToString("yyyy-dd-MM hh:mm:ss") & "] [Fin] - All task done, reschedule all task")
                ResetData()
                LoadNewData()
            End If

        Catch e As Exception
            Console.WriteLine("Error: {0}", e.ToString())
        Finally
            If conn IsNot Nothing Then
                conn.Close()
            End If
        End Try
    End Sub

    Sub Main()
        'For i As Integer = 1 To 5
        '    Dim ip As Int16 = i
        '    Threading.ThreadPool.QueueUserWorkItem(Function() Logfetch("172.30.241.4" & ip, "bus-" & ip))
        'Next
        Console.Title = "Mikrotik Log Grabber v1.2"
        LoadNewData()

        Console.ReadLine()
    End Sub
End Module
