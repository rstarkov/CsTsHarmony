import { APIRequestContext, APIResponse } from '@playwright/test';

export interface ApiServiceOptions {
    request: APIRequestContext;
}

interface RequestArgs {
    method: string;
    body?: string;
    headers?: { [key: string]: string };
}

export class ApiServiceBase {

    protected request: APIRequestContext;

    public constructor(options?: ApiServiceOptions) {
        if (!options?.request)
            throw new Error();
        this.request = options.request;
    }

    private async fetch(url: string, opts: RequestArgs): Promise<APIResponse> {
        let resp = await this.request[opts.method.toLowerCase()](url, { data: opts.body, headers: opts.headers });
        if (resp.status() < 200 || resp.status() >= 300)
            throw new Error();
        return resp;
    }

    protected async fetchJson(url: string, opts: RequestArgs): Promise<any> {
        let resp = await this.fetch(url, opts);
        return resp.json();
    }

    protected async fetchString(url: string, opts: RequestArgs): Promise<any> {
        let resp = await this.fetch(url, opts);
        return resp.text();
    }

    protected async fetchVoid(url: string, opts: RequestArgs): Promise<any> {
        let resp = await this.fetch(url, opts);
        if (resp.headers()['content-length'] != '0')
            throw new Error();
    }
}
