import {
  forwardRef,
  Inject,
  Injectable,
  UnprocessableEntityException,
} from '@nestjs/common';
import { CreateAuthenticationDto } from './dto/create-authentication.dto';
import { UpdateAuthenticationDto } from './dto/update-authentication.dto';
import { UsersService } from 'src/users/users.service';
import { User } from 'src/users/entities/user.entity';
import { CreateUserDto } from 'src/users/dto/create-user.dto';
import { hash, compare } from 'bcrypt';
import { JwtService } from '@nestjs/jwt';
import { HttpErrorByCode } from '@nestjs/common/utils/http-error-by-code.util';
import { Request } from 'express';

interface Payload {
  email: string;
  sub: string;
  firstName: string;
  lastName: string;
}

@Injectable()
export class AuthenticationService {
  saltRouds: number;
  constructor(
    @Inject(forwardRef(() => UsersService))
    private usersService: UsersService,
    private readonly jwtService: JwtService,
  ) {
    this.saltRouds = 10;
  }

  create(createAuthenticationDto: CreateAuthenticationDto) {
    return 'This action adds a new authentication';
  }

  findAll() {
    return `This action returns all authentication`;
  }

  findOne(id: number) {
    return `This action returns a #${id} authentication`;
  }

  update(id: number, updateAuthenticationDto: UpdateAuthenticationDto) {
    return `This action updates a #${id} authentication`;
  }

  remove(id: number) {
    return `This action removes a #${id} authentication`;
  }

  async encryptPassword(password: CreateUserDto['password']) {
    return await hash(password, this.saltRouds);
  }

  async validateUser(
    email: string,
    pass: string,
  ): Promise<Partial<User> | null> {
    const user = await this.usersService.findOneByEmail(email);
    if (user && (await compare(pass, user.password))) {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { password, ...result } = user;
      return result;
    }
    return null;
  }

  async login(user: User) {
    const payload = this.buildPayload(user);
    return {
      access_token: this.jwtService.sign(payload),
    };
  }

  async refreshToken(accessToken?: Request['headers']['authorization']) {
    if (!accessToken)
      throw new UnprocessableEntityException(
        HttpErrorByCode[422],
        'A token is required in order to refresh it with a new one',
      );
    // decode the token
    const decodedAccessToken: Payload = this.jwtService.decode(
      accessToken.split(' ')[1],
    );
    const user = await this.usersService.findOne(decodedAccessToken.sub);
    const payload = this.buildPayload(user);
    return {
      access_token: this.jwtService.sign(payload),
    };
  }

  private buildPayload(user: User): Payload {
    return {
      email: user.email,
      sub: user.id,
      firstName: user.firstName,
      lastName: user.lastName,
    };
  }
}
