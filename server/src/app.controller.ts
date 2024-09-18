import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Post,
  Request,
  UseGuards,
} from '@nestjs/common';
import { AppService } from './app.service';
import { LocalAuthGuard } from './authentication/local-authentication.guard';
import { CreateUserDto } from './users/dto/create-user.dto';
import { UsersService } from './users/users.service';
import { AuthenticationService } from './authentication/authentication.service';
import { Public } from './authentication/authentication.decorator';
import { UpdateUserPasswordDto } from './users/dto/update-user.dto';
import { Request as IRequest } from 'express';

@Controller()
export class AppController {
  constructor(
    private readonly appService: AppService,
    private readonly usersService: UsersService,
    private readonly authService: AuthenticationService,
  ) {}

  @Public()
  @Get()
  getHello(): string {
    return this.appService.getHello();
  }

  @Public()
  @Post('authentication/register')
  async register(@Body() createUserDto: CreateUserDto) {
    this.usersService.create(createUserDto);
  }

  @Public()
  @HttpCode(HttpStatus.OK)
  @UseGuards(LocalAuthGuard)
  @Post('authentication/login')
  async login(@Request() req) {
    return this.authService.login(req.user);
  }

  @HttpCode(HttpStatus.OK)
  @Post('authentication/reset-password')
  async refreshAccessToken(@Request() req: IRequest) {
    await this.authService.refreshToken(req.headers.authorization);
  }

  @HttpCode(HttpStatus.OK)
  @Post('authentication/reset-password')
  async resetPassword(@Body() updateUserPasswordDto: UpdateUserPasswordDto) {
    await this.usersService.updatePassword(updateUserPasswordDto);
  }
}
