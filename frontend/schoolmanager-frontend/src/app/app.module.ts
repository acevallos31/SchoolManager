import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { AppComponent } from './app.component';
import { AppHomeComponent } from './app-home.component';
import { JwtInterceptor } from './core/interceptors/jwt.interceptor';
import { AppRoutingModule } from './app-routing.module';

@NgModule({
  declarations: [AppComponent, AppHomeComponent],
  imports: [BrowserModule, HttpClientModule, RouterModule, AppRoutingModule],
  providers: [
    {
      provide: HTTP_INTERCEPTORS,
      useClass: JwtInterceptor,
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule {}
