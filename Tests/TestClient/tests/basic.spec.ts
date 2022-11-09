import { test, expect } from '@playwright/test';
import { Services } from '../api';

test('post void', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.PostVoid()).toEqual(undefined);
});

test('get model', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetModel()).toEqual({ q1: "foo", r1: 123, q2: true, r2: "" });
});

test('get model arr', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetModelArr()).toEqual([{ q1: "foo", r1: 123, q2: true, r2: "" }, { q1: "bar", r1: 124, q2: false, r2: "oof" }]);
});

test('get model arr 0', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetModelArr0()).toEqual([]);
});

test('get int', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetInt()).toEqual(47);
});

test('get int 0', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetInt0()).toEqual(0);
});

test('get string', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetString()).toEqual("owr");
});

test('get string 0', async ({ request }) => {
    let svc = new Services({ request });
    expect(await svc.BasicStrict.GetString0()).toEqual("");
});

// test('get enum', async ({ request }) => {
//     let svc = new Services({ request });
//     expect(await svc.BasicStrict.GetEnum()).toEqual(HarmonyTests.TestEnum.Blah); // with default ASP config
// });

test('query only', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.QueryOnly("foo", true)).toEqual('BasicStrict/qonly?q1=foo&q2=true'); // relative path
    expect(await svc.BasicStrict.QueryOnly("foo", true)).toEqual({ q1: "foo", q2: true, r1: 0, r2: "" });
});

test('query and route', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.QueryAndRoute("asd", 47, false, "25")).toEqual('qandr/47/foo/25/bar?q1=asd&q2=false');
    expect(await svc.BasicStrict.QueryAndRoute("asd", 47, false, "25")).toEqual({ q1: "asd", r1: 47, q2: false, r2: "25" });
});

// test('query and route 2', async ({ request }) => { // must be optional
//     let svc = new Services({ request });
//     expect(svc.BasicStrict.endpoints.QueryAndRoute("", 47, false, "25")).toEqual('qandr/47/foo/25/bar?q1=&q2=false');
//     expect(await svc.BasicStrict.QueryAndRoute("", 47, false, "25")).toEqual({ q1: "", r1: 47, q2: false, r2: "25" });
// });

test('query array', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.QueryArray("foo", ["bar", "", "baz"])).toEqual('qarr?q1=foo&qa=bar&qa=&qa=baz');
    expect(await svc.BasicStrict.QueryArray("foo", ["bar", "", "baz"])).toEqual(["foo", "bar", "", "baz"]);
});

test('body array', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.BodyArray("foo", ["bar", "", "baz"])).toEqual('barr?q1=foo');
    expect(await svc.BasicStrict.BodyArray("foo", ["bar", "", "baz"])).toEqual(["foo", "bar", "", "baz"]);
});

test('query route body', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.QueryRouteBody("Foo", 25, true, "baZ")).toEqual('qandrandb/baZ?q1=Foo&q2=true');
    expect(await svc.BasicStrict.QueryRouteBody("Foo", 25, true, "baZ")).toEqual({ q1: "Foo", r1: 25, q2: true, r2: "baZ" });
});

// test('query route body 2', async ({ request }) => { // must be optional
//     let svc = new Services({ request });
//     expect(svc.BasicStrict.endpoints.QueryRouteBody("Foo", 25, true, "")).toEqual('qandrandb/?q1=Foo&q2=true');
//     expect(await svc.BasicStrict.QueryRouteBody("Foo", 25, true, "")).toEqual({ q1: "Foo", r1: 25, q2: true, r2: "" });
// });

test('model body', async ({ request }) => {
    let svc = new Services({ request });
    let model: HarmonyTests.FooResult = { q1: "fwih", q2: true, r1: 123, r2: "fskh" };
    expect(svc.BasicStrict.endpoints.ModelBody(model)).toEqual('modelbody');
    expect(await svc.BasicStrict.ModelBody(model)).toEqual(model);
});

test('model query', async ({ request }) => {
    let svc = new Services({ request });
    let model: HarmonyTests.FooResult = { q1: "fwih", q2: true, r1: 123, r2: "fskh" };
    expect(svc.BasicStrict.endpoints.ModelQuery(model)).toEqual('modelquery?q1=fwih&q2=true&r1=123&r2=fskh');
    expect(await svc.BasicStrict.ModelQuery(model)).toEqual(model);
});

// test('model form', async ({ request }) => {
//     let svc = new Services({ request });
//     let model: HarmonyTests.FooResult = { q1: "fwih", q2: true, r1: 123, r2: "fskh" };
//     expect(svc.BasicStrict.endpoints.ModelForm(model)).toEqual('modelform');
//     expect(await svc.BasicStrict.ModelForm(model)).toEqual(model);
// });

test('overloaded', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.Overloaded1_1("baz")).toEqual('overloaded1a?p1=baz');
    expect(await svc.BasicStrict.Overloaded1_1("baz")).toEqual("fooA:baz");
    expect(svc.BasicStrict.endpoints.Overloaded1_2("baz", 21)).toEqual('overloaded1b?p1=baz&p2=21');
    expect(await svc.BasicStrict.Overloaded1_2("baz", 21)).toEqual("fooB:baz,21");
});

test('samename', async ({ request }) => {
    let svc = new Services({ request });
    expect(svc.BasicStrict.endpoints.SameName1Get()).toEqual('samename');
    expect(svc.BasicStrict.endpoints.SameName2Post()).toEqual('samename');
    expect(await svc.BasicStrict.SameName1Get()).toEqual("foo1");
    expect(await svc.BasicStrict.SameName2Post()).toEqual("foo2");
});

