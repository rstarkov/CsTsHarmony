using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace HarmonyTests;

#nullable enable

public class FooResult
{
    public string q1 { get; set; } = "";
    public int r1 { get; set; }
    public bool q2 { get; set; }
    public string r2 { get; set; } = "";
}

public enum TestEnum { Foo = 1, Blah }

[ApiController]
[Route("[controller]")]
public class BasicStrictController : ControllerBase
{
    [HttpGet("/getmodel")]
    public FooResult GetModel()
    {
        return new FooResult { q1 = "foo", r1 = 123, q2 = true, r2 = "" };
    }

    [HttpGet("/getmodelarr")]
    public IEnumerable<FooResult> GetModelArr()
    {
        return new[] { new FooResult { q1 = "foo", r1 = 123, q2 = true, r2 = "" }, new FooResult { q1 = "bar", r1 = 124, q2 = false, r2 = "oof" } };
    }

    [HttpGet("/getmodelarr0")]
    public IEnumerable<FooResult> GetModelArr0() => new FooResult[0];

    [HttpGet("/getint")]
    public int GetInt() => 47;

    [HttpGet("/getint0")]
    public int GetInt0() => 0;

    [HttpGet("/getstring")]
    public string GetString() => "owr";

    [HttpGet("/getstring0")]
    public string GetString0() => "";

    [HttpGet("/getenum")]
    public TestEnum GetEnum() => TestEnum.Blah;

    [HttpGet("/getbinary")]
    public IActionResult GetBinary()
    {
        return File(Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA"), "image/webp");
    }

    [HttpGet("qonly")] // relative path
    public FooResult QueryOnly(string q1, bool q2)
    {
        return new FooResult { q1 = q1, q2 = q2 };
    }

    [HttpGet("/qandr/{r1:int}/foo/{r2}/bar")]
    public FooResult QueryAndRoute(string q1, int r1, bool q2, string r2)
    {
        return new FooResult { q1 = q1, r1 = r1, q2 = q2, r2 = r2 };
    }

    [HttpGet("/qarr")]
    public string[] QueryArray(string q1, [FromQuery] string[] qa)
    {
        return new[] { q1 }.Concat(qa).ToArray();
    }

    [HttpGet("/barr")]
    public string[] BodyArray(string q1, [FromBody] string[] qa)
    {
        return new[] { q1 }.Concat(qa).ToArray();
    }

    [HttpGet("/qandrandb/{r2}")]
    public FooResult QueryRouteBody(string q1, [FromBody] int r1, bool q2, string r2)
    {
        return new FooResult { q1 = q1, r1 = r1, q2 = q2, r2 = r2 };
    }

    [HttpPost("/modelbody")]
    public FooResult ModelBody([FromBody] FooResult foo) // error if a property is missing
    {
        return foo;
    }

    [HttpPost("/modelquery")]
    public FooResult ModelQuery([FromQuery] FooResult foo) // error if a property is missing
    {
        return foo;
    }

    [HttpPost("/modelform")]
    public FooResult ModelForm([FromForm] FooResult foo) // error if a property is missing
    {
        return foo;
    }

    [HttpGet("/overloaded1a")]
    public string Overloaded1(string p1)
    {
        return $"fooA:{p1}";
    }

    [HttpGet("/overloaded1b")]
    public string Overloaded1(string p1, int p2)
    {
        return $"fooB:{p1},{p2}";
    }

    [HttpGet("/samename", Name = "SameNameA")]
    public string SameName1Get()
    {
        return "foo1";
    }

    [HttpPost("/samename", Name = "SameNameA")]
    public string SameName2Post()
    {
        return "foo2";
    }
}
