#ifndef UNICODE
#define UNICODE
#endif
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <imm.h>
#include <shlobj.h>
#include <strsafe.h>
#include <string.h>
#include <wchar.h>
#include <time.h>

#define APP_NAME L"必应轻壁纸"
#define HOST_CLASS L"BingWallpaperLite.NativeHost.1"
#define TRAY_MSG (WM_APP+1)
#define SHOW_MSG (WM_APP+2)
#define QUIT_MSG (WM_APP+3)
#define BUSY_MSG (WM_APP+4)
#define RELOAD_MSG (WM_APP+5)
#define NEXT_MSG (WM_APP+6)
#define ID_UPDATE 101
#define ID_FOLDER 103
#define ID_LOG 104
#define ID_QUIT 105
#define ID_SHOW 106

static HINSTANCE instance;
static HWND host;
static NOTIFYICONDATAW tray;
static HANDLE worker;
static int quitPending, autoUpdate=1, hour=8, minute=5;
static wchar_t root[32768], helperPath[32768];
static wchar_t status[160]=L"等待更新";
static wchar_t settingsProperty[96];
static time_t nextCheck;
static UINT taskbarCreated;

#include "config.h"

static void loadConfig(void)
{
    wchar_t path[32768], value[64];
    if (FAILED(StringCchPrintfW(path, ARRAYSIZE(path), L"%ls\\config.json", root))) return;
    char *json = readFile(path);
    if (jsonValue(json, L"DailyTime", value, ARRAYSIZE(value))) parseDailyTime(value, &hour, &minute);
    if (jsonValue(json, L"AutoUpdate", value, ARRAYSIZE(value))) {
        if (wcscmp(value, L"true") == 0) autoUpdate = 1;
        else if (wcscmp(value, L"false") == 0) autoUpdate = 0;
    }
    if (json) HeapFree(GetProcessHeap(), 0, json);
}
static time_t nextDailyAt(time_t now,int h,int m)
{
    struct tm value;localtime_s(&value,&now);
    value.tm_hour=h;value.tm_min=m;value.tm_sec=0;value.tm_isdst=-1;
    time_t next=mktime(&value);if(next<=now){value.tm_mday++;value.tm_isdst=-1;next=mktime(&value);}return next;
}
static time_t nextDaily(void){return nextDailyAt(time(NULL),hour,minute);}
static HWND findHost(void)
{
    return FindWindowW(HOST_CLASS, root);
}
static BOOL CALLBACK findSettingsCallback(HWND window,LPARAM result)
{
    if(GetPropW(window,settingsProperty)){*(HWND*)result=window;return FALSE;}return TRUE;
}
static HWND findSettings(void){HWND window=NULL;EnumWindows(findSettingsCallback,(LPARAM)&window);return window;}
static void refresh(void)
{
    StringCchPrintfW(tray.szTip, ARRAYSIZE(tray.szTip), L"必应轻壁纸 · %.48ls", status);
    Shell_NotifyIconW(NIM_MODIFY, &tray);
}
static HANDLE launchHelper(const wchar_t *mode)
{
    wchar_t command[32768];
    if (FAILED(StringCchPrintfW(command, ARRAYSIZE(command), L"\"%ls\" %ls --data-dir \"%ls\"", helperPath, mode, root))) {
        SetLastError(ERROR_INSUFFICIENT_BUFFER);
        return NULL;
    }
    STARTUPINFOW startup = { sizeof(startup) };
    PROCESS_INFORMATION process = {0};
    if (!CreateProcessW(helperPath, command, NULL, NULL, FALSE, 0, NULL, NULL, &startup, &process)) return NULL;
    CloseHandle(process.hThread);
    return process.hProcess;
}
static void startUpdate(int force)
{
    if (worker) return;
    worker = launchHelper(force ? L"--force" : L"--update");
    if (!worker) {
        StringCchPrintfW(status, ARRAYSIZE(status), L"无法启动更新模块（错误 %lu）", GetLastError());
        nextCheck = time(NULL) + 900;
    } else {
        wcscpy(status, L"正在同步桌面与锁屏…");
        SetTimer(host, 1, 1000, NULL);
    }
    refresh();
}
static void openFolder(void){wchar_t path[32768];StringCchPrintfW(path,32768,L"%ls\\images",root);SHCreateDirectoryExW(NULL,path,NULL);ShellExecuteW(NULL,L"open",path,NULL,NULL,SW_SHOWNORMAL);}
static void openLog(void){wchar_t path[32768];StringCchPrintfW(path,32768,L"%ls\\update.log",root);ShellExecuteW(NULL,L"open",L"notepad.exe",path,NULL,SW_SHOWNORMAL);}
static void showWindow(void)
{
    HWND existing = findSettings();
    if (existing) { ShowWindow(existing, SW_RESTORE); SetForegroundWindow(existing); return; }
    HANDLE process = launchHelper(L"--settings");
    if (process) CloseHandle(process);
}
static void quit(void)
{
    if (worker) {
        quitPending = 1;
        wcscpy(status, L"完成本次更新后退出…");
        refresh();
    } else DestroyWindow(host);
}

static void checkUpdate(void)
{
    if (!worker) {
        if (autoUpdate && time(NULL) >= nextCheck) startUpdate(0);
        return;
    }
    DWORD code = STILL_ACTIVE;
    if (!GetExitCodeProcess(worker, &code) || code == STILL_ACTIVE) return;
    CloseHandle(worker);
    worker = NULL;
    nextCheck = code == 0 ? nextDaily() : time(NULL) + 900;
    wcscpy(status, code == 0 ? L"桌面与锁屏已同步" : L"更新失败，15 分钟后重试");
    SetTimer(host, 1, 60000, NULL);
    refresh();
    if (quitPending) DestroyWindow(host);
}

static void showTrayMenu(HWND window)
{
    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING, ID_SHOW, L"打开设置");
    AppendMenuW(menu, MF_STRING | (worker ? MF_GRAYED : 0), ID_UPDATE, L"立即更新桌面与锁屏");
    AppendMenuW(menu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(menu, MF_STRING, ID_FOLDER, L"打开图片文件夹");
    AppendMenuW(menu, MF_STRING, ID_LOG, L"查看日志");
    AppendMenuW(menu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(menu, MF_STRING, ID_QUIT, L"退出");
    POINT cursor;
    GetCursorPos(&cursor);
    SetForegroundWindow(window);
    UINT id = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, cursor.x, cursor.y, 0, window, NULL);
    DestroyMenu(menu);
    switch (id) {
    case ID_SHOW: showWindow(); break;
    case ID_UPDATE: startUpdate(1); break;
    case ID_FOLDER: openFolder(); break;
    case ID_LOG: openLog(); break;
    case ID_QUIT: quit(); break;
    }
    PostMessageW(window, WM_NULL, 0, 0);
}
static LRESULT CALLBACK HostProc(HWND hwnd,UINT msg,WPARAM w,LPARAM l)
{
    if(msg==taskbarCreated){Shell_NotifyIconW(NIM_ADD,&tray);return 0;}
    if(msg==SHOW_MSG){showWindow();return 0;}
    if(msg==QUIT_MSG){quit();return 0;}
    if(msg==BUSY_MSG)return worker!=NULL;
    if(msg==NEXT_MSG)return(LRESULT)nextCheck;
    if(msg==RELOAD_MSG){loadConfig();nextCheck=nextDaily();wcscpy(status,autoUpdate?L"设置已保存":L"自动更新已暂停");refresh();return 0;}
    if(msg==WM_COMMAND&&w==ID_UPDATE){startUpdate(1);return 0;}
    if (msg == WM_TIMER) { checkUpdate(); return 0; }
    if(msg==WM_POWERBROADCAST&&(w==PBT_APMRESUMEAUTOMATIC||w==PBT_APMRESUMESUSPEND)){
        if(autoUpdate&&!worker&&time(NULL)>=nextCheck)startUpdate(0);return TRUE;
    }
    if(msg==TRAY_MSG){
        if(l==WM_LBUTTONDBLCLK)showWindow();
        if (l == WM_RBUTTONUP || l == WM_CONTEXTMENU) showTrayMenu(hwnd);
        return 0;
    }
    if(msg==WM_QUERYENDSESSION)return TRUE;
    if(msg==WM_ENDSESSION&&w){Shell_NotifyIconW(NIM_DELETE,&tray);return 0;}
    if(msg==WM_DESTROY){Shell_NotifyIconW(NIM_DELETE,&tray);HWND settings=findSettings();if(settings)PostMessageW(settings,WM_CLOSE,0,0);PostQuitMessage(0);return 0;}
    return DefWindowProcW(hwnd,msg,w,l);
}
int WINAPI wWinMain(HINSTANCE inst,HINSTANCE previous,LPWSTR command,int show)
{
    (void)previous;(void)command;(void)show;instance=inst;
    // The tray host has no text inputs. Keep IME/TSF extensions out of this process;
    // the separate settings process retains its normal input method support.
    ImmDisableIME((DWORD)-1);
    int argc;wchar_t **argv=CommandLineToArgvW(GetCommandLineW(),&argc);int hidden=0,exitOnly=0,force=0,noUpdate=0;
    SHGetFolderPathW(NULL,CSIDL_LOCAL_APPDATA,NULL,SHGFP_TYPE_CURRENT,root);wcscat(root,L"\\BingWallpaperLite");
    for(int i=1;i<argc;i++){
        if(wcscmp(argv[i],L"--data-dir")==0&&i+1<argc)wcsncpy(root,argv[++i],32760);
        else if(wcscmp(argv[i],L"--tray")==0)hidden=1;else if(wcscmp(argv[i],L"--quit")==0)exitOnly=1;
        else if(wcscmp(argv[i],L"--no-update")==0)noUpdate=1;else if(wcscmp(argv[i],L"--force")==0||wcscmp(argv[i],L"--update")==0)force=1;
    }
    LocalFree(argv);host=findHost();
    if(exitOnly){if(!host)return 0;DWORD pid;GetWindowThreadProcessId(host,&pid);HANDLE process=OpenProcess(SYNCHRONIZE,FALSE,pid);PostMessageW(host,QUIT_MSG,0,0);DWORD wait=process?WaitForSingleObject(process,15000):WAIT_OBJECT_0;if(process)CloseHandle(process);return wait==WAIT_OBJECT_0?0:1;}
    if(host){if(force)PostMessageW(host,WM_COMMAND,ID_UPDATE,0);else if(!hidden)PostMessageW(host,SHOW_MSG,0,0);return 0;}
    GetModuleFileNameW(NULL, helperPath, ARRAYSIZE(helperPath));
    wchar_t *slash = wcsrchr(helperPath, '\\');
    if (!slash || FAILED(StringCchCopyW(slash + 1, ARRAYSIZE(helperPath) - (slash + 1 - helperPath), L"BingWallpaperHelper.exe"))) return 1;
    SHCreateDirectoryExW(NULL, root, NULL);
    loadConfig();
    if (force) {
        HANDLE process = launchHelper(L"--force");
        if (!process) return 1;
        WaitForSingleObject(process, INFINITE);
        DWORD code = 1;
        GetExitCodeProcess(process, &code);
        CloseHandle(process);
        return (int)code;
    }
    unsigned long long hash=1469598103934665603ULL;for(const wchar_t *p=root;*p;p++){wchar_t c=*p>='A'&&*p<='Z'?*p+32:*p;hash^=(unsigned short)c;hash*=1099511628211ULL;}
    StringCchPrintfW(settingsProperty,96,L"BingWallpaperLite.Settings.%016I64x",hash);
    wchar_t name[96];StringCchPrintfW(name,96,L"Local\\BingWallpaperLite.Native.%016I64x",hash);HANDLE singleton=CreateMutexW(NULL,TRUE,name);
    if(!singleton)return 1;
    if(GetLastError()==ERROR_ALREADY_EXISTS){DWORD acquired=WaitForSingleObject(singleton,0);if(acquired!=WAIT_OBJECT_0&&acquired!=WAIT_ABANDONED){CloseHandle(singleton);return 0;}}
    WNDCLASSW cls={0};cls.hInstance=instance;cls.lpfnWndProc=HostProc;cls.lpszClassName=HOST_CLASS;RegisterClassW(&cls);
    taskbarCreated=RegisterWindowMessageW(L"TaskbarCreated");
    host=CreateWindowExW(WS_EX_TOOLWINDOW,HOST_CLASS,root,0,0,0,0,0,NULL,NULL,instance,NULL);
    if(!host){ReleaseMutex(singleton);CloseHandle(singleton);return 1;}
    tray.cbSize=sizeof(tray);tray.hWnd=host;tray.uID=1;tray.uFlags=NIF_MESSAGE|NIF_ICON|NIF_TIP;tray.uCallbackMessage=TRAY_MSG;tray.hIcon=LoadIconW(instance,MAKEINTRESOURCEW(1));wcscpy(tray.szTip,APP_NAME);Shell_NotifyIconW(NIM_ADD,&tray);
    nextCheck=noUpdate?nextDaily():time(NULL);SetTimer(host,1,!noUpdate&&autoUpdate?1000:60000,NULL);
    if(!hidden)showWindow();MSG message;while(GetMessageW(&message,NULL,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}
    ReleaseMutex(singleton);CloseHandle(singleton);return 0;
}
