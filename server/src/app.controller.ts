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
  @HttpCode(HttpStatus.OK)
  @UseGuards(LocalAuthGuard)
  @Post('authentication/login')
  async login(@Request() req) {
    return this.authService.login(req.user);
  }

  @Public()
  @Post('authentication/register')
  async register(@Body() createUserDto: CreateUserDto) {
    this.usersService.create(createUserDto);
  }
}
