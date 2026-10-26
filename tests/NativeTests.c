#include "../native/app.c"
#include <assert.h>
#include <stdio.h>
int main(void)
{
    wchar_t out[128];
    assert(jsonValue("{\"Title\":\"\\u4e2d\\u6587\"}",L"Title",out,128)&&wcscmp(out,L"中文")==0);
    assert(jsonValue("{\"Image\":\"C:\\\\images\\\\bing.jpg\"}",L"Image",out,128)&&wcscmp(out,L"C:\\images\\bing.jpg")==0);
    assert(jsonValue("{\"AutoUpdate\":false,\"KeepImages\":1}",L"AutoUpdate",out,128)&&wcscmp(out,L"false")==0);
    assert(jsonValue("{\"AutoUpdate\":true,\"KeepImages\":3}",L"KeepImages",out,128)&&wcscmp(out,L"3")==0);
    assert(!jsonValue("{\"Title\":\"truncated",L"Title",out,128));
    assert(!jsonValue("{\"Title\":\"\\u12",L"Title",out,128));
    assert(!jsonValue("{\"Other\":1}",L"Title",out,128));
    assert(jsonValue("{\"Title\":\"中文壁纸\"}",L"Title",out,128)&&wcscmp(out,L"中文壁纸")==0);
    assert(jsonValue("{\"Title\":\"abcdef\"}",L"Title",out,4)&&wcscmp(out,L"abc")==0);
    struct tm now={0},expected={0};now.tm_year=expected.tm_year=126;now.tm_mon=expected.tm_mon=9;now.tm_mday=expected.tm_mday=26;now.tm_hour=7;expected.tm_hour=8;expected.tm_min=5;now.tm_isdst=expected.tm_isdst=-1;
    assert(nextDailyAt(mktime(&now),8,5)==mktime(&expected));
    now=expected;expected.tm_mday=27;assert(nextDailyAt(mktime(&now),8,5)==mktime(&expected));
    now.tm_year=126;now.tm_mon=11;now.tm_mday=31;now.tm_hour=23;now.tm_min=59;now.tm_sec=0;now.tm_isdst=-1;
    expected.tm_year=127;expected.tm_mon=0;expected.tm_mday=1;expected.tm_hour=8;expected.tm_min=5;expected.tm_sec=0;expected.tm_isdst=-1;
    assert(nextDailyAt(mktime(&now),8,5)==mktime(&expected));
    int h=0,m=0;
    assert(parseDailyTime(L"23:59",&h,&m)&&h==23&&m==59);
    assert(parseDailyTime(L"00:00",&h,&m)&&h==0&&m==0);
    assert(!parseDailyTime(L"24:00",&h,&m));
    assert(!parseDailyTime(L"8:05",&h,&m));
    assert(!parseDailyTime(L"08:05x",&h,&m));
    assert(!parseDailyTime(L"08:60",&h,&m));
    assert(jsonValue("{\"Other\":\"skip me\",\"AutoUpdate\":false  }",L"AutoUpdate",out,128)&&wcscmp(out,L"false")==0);
    assert(!jsonValue("{\"Title\":\"bad\\q\"}",L"Title",out,128));
    assert(SUCCEEDED(StringCchPrintfW(out,128,L"%016I64x",0x0123456789abcdefULL))&&wcscmp(out,L"0123456789abcdef")==0);
    puts("PASS: 21 native parser, formatting and daily scheduling assertions.");
    return 0;
}
