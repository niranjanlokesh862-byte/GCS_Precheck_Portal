import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PreCheckJobResponse, PreCheckJobStatus, StartPreCheckRequest } from '../models/precheck.models';

@Injectable({
  providedIn: 'root'
})
export class PreCheckService {
  private readonly baseUrl = '/api/precheck';

  constructor(private http: HttpClient) {}

  public startPreCheck(request: StartPreCheckRequest): Observable<PreCheckJobResponse> {
    return this.http.post<PreCheckJobResponse>(`${this.baseUrl}/start`, request);
  }

  public getJobStatus(jobId: string): Observable<PreCheckJobStatus> {
    return this.http.get<PreCheckJobStatus>(`${this.baseUrl}/jobs/${jobId}`);
  }
}
