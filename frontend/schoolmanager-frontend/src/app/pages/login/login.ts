import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  correo = '';
  password = '';
  error = '';
  cargando = false;

  constructor(private auth: AuthService, private router: Router) {}

  async login() {
    this.error = '';
    this.cargando = true;
    try {
      await this.auth.login(this.correo, this.password);
      this.router.navigate(['/dashboard']);
    } catch (e: any) {
      this.error = 'Correo o contraseña incorrectos';
    } finally {
      this.cargando = false;
    }
  }
}